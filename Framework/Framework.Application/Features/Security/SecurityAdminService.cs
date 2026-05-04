using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Security;

// 보안 화면 통합 서비스 구현체
// 기존 AdminSecurityController.GetTimeline 본문 로직을 Application 레이어로 이전
// 조회 전용이므로 트랜잭션/UoW 불필요
public class SecurityAdminService : ISecurityAdminService
{
    // Rate Limit 로그 저장소 — IP 집계 및 타임라인 조회에 사용
    private readonly IRateLimitLogRepository _rateLimitLogRepo;

    // 감사 로그 저장소 — 이상치(IsAnomaly) 이벤트 타임라인 조회에 사용
    private readonly IAuditLogRepository _auditLogRepo;

    // 플레이어 저장소 — 밴 목록 및 밴 여부 일괄 조회에 사용
    private readonly IPlayerRepository _playerRepo;

    public SecurityAdminService(
        IRateLimitLogRepository rateLimitLogRepo,
        IAuditLogRepository auditLogRepo,
        IPlayerRepository playerRepo)
    {
        _rateLimitLogRepo = rateLimitLogRepo;
        _auditLogRepo = auditLogRepo;
        _playerRepo = playerRepo;
    }

    // IP별 Rate Limit 집계 — Repository 호출 후 DTO로 변환
    public async Task<List<RateLimitIpAggregateDto>> GetRateLimitAggregatedByIpAsync()
    {
        var aggregated = await _rateLimitLogRepo.GetAggregatedByIpAsync();

        // 튜플 → DTO 변환 (필드명·타입·순서를 Razor 측 RateLimitLogDto와 일치시킴)
        return aggregated
            .Select(x => new RateLimitIpAggregateDto(x.IpAddress, x.Count, x.LastOccurredAt))
            .ToList();
    }

    // 보안 통합 타임라인 조회
    // ① Rate Limit 초과 이벤트 + ② AuditLog 이상치 이벤트 + ③ 밴 처리 플레이어를 하나의 타임라인으로 조합
    // 기존 AdminSecurityController.GetTimeline 본문을 그대로 이전 — 누락/오역 없음
    public async Task<List<SecurityTimelineItemDto>> GetTimelineAsync(
        DateTime? from, DateTime? to, int? playerId, string? ip)
    {
        // 기간 기본값: 미입력 시 최근 7일 (컨트롤러에서 UTC 변환 후 전달, null이면 여기서 처리)
        var fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-7);
        var toUtc   = to?.ToUniversalTime()   ?? DateTime.UtcNow;

        // ① Rate Limit 초과 이벤트 — 기간 + playerId/ip 필터 적용, 최대 200건
        var rateLimitRaw = await _rateLimitLogRepo.GetRecentByFiltersAsync(
            fromUtc, toUtc, playerId, ip, take: 200);

        // ② AuditLog 이상치 이벤트 — IsAnomaly=true 강제, 기간 + playerId 필터, 최대 200건
        var anomalyRaw = await _auditLogRepo.GetRecentAnomaliesAsync(
            fromUtc, toUtc, playerId, take: 200);

        // ③ 활성 밴 플레이어 — playerId 필터 적용, 최대 100건 (BannedUntil 내림차순)
        var banPlayers = await _playerRepo.GetActiveBansAsync(playerId, take: 100);

        // 밴 항목 DTO 변환 — BannedUntil null이면 영구밴
        var banItems = banPlayers.Select(p => new SecurityTimelineItemDto(
            p.BannedUntil ?? DateTime.UtcNow,
            "Ban",
            p.Id,
            null,
            p.BannedUntil == null
                ? $"영구 정지 — 닉네임: {p.Nickname}"
                : $"기간 정지 (~ {p.BannedUntil.Value:yyyy-MM-dd HH:mm} UTC) — 닉네임: {p.Nickname}",
            "Critical",
            IsBanned: true));

        // RateLimit + Anomaly 항목의 PlayerId 배치 조회 — IsBanned 표시용
        var playerIds = rateLimitRaw.Select(l => l.PlayerId)
            .Concat(anomalyRaw.Select(l => (int?)l.PlayerId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        // 배치 조회: 주어진 PlayerId 집합 중 IsBanned=true인 ID 집합 반환
        var bannedSet = await _playerRepo.GetBannedIdsAsync(playerIds);

        // Rate Limit 이벤트 DTO 변환
        var rateLimitItems = rateLimitRaw.Select(l => new SecurityTimelineItemDto(
            l.OccurredAt, "RateLimit", l.PlayerId, l.IpAddress,
            $"Rate Limit 초과 — 경로: {l.Path}, 정책: {l.Policy}",
            "Warn",
            IsBanned: l.PlayerId.HasValue && bannedSet.Contains(l.PlayerId.Value)));

        // Anomaly 이벤트 DTO 변환
        var anomalyItems = anomalyRaw.Select(l => new SecurityTimelineItemDto(
            l.CreatedAt, "Anomaly", l.PlayerId, null,
            $"재화 이상치 — 아이템 ID: {l.ItemId}, 변동: {l.ChangeAmount:+#;-#;0}, 사유: {l.Reason}",
            "Warn",
            IsBanned: bannedSet.Contains(l.PlayerId)));

        // 3개 소스 병합 후 시간 내림차순 정렬, 최대 200건 반환
        return rateLimitItems
            .Concat(anomalyItems)
            .Concat(banItems)
            .OrderByDescending(e => e.OccurredAt)
            .Take(200)
            .ToList();
    }
}
