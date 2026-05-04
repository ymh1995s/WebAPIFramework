using Framework.Api.Filters;
using Framework.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 보안 통합 타임라인 컨트롤러 — X-Admin-Key 헤더 검증 자동 적용
[AdminApiKey]
[ApiController]
[Route("api/admin/security")]
public class AdminSecurityController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AdminSecurityController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // Rate Limit 정책 현재 설정값 반환 — Admin 페이지 동적 표시용
    // auth 정책은 인증 여부에 따라 한도가 분기되므로 두 값을 모두 반환
    [HttpGet("rate-limit-config")]
    public IActionResult GetRateLimitConfig()
    {
        return Ok(new RateLimitConfigDto(
            // 미인증(IP) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPermitLimit 키
            AuthPermitLimit: _config.GetValue<int>("RateLimiting:AuthPermitLimit", 15),
            // 인증(PlayerId) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPlayerPermitLimit 키
            AuthPlayerPermitLimit: _config.GetValue<int>("RateLimiting:AuthPlayerPermitLimit", 30)
        ));
    }

    // 보안 이벤트 통합 타임라인 조회
    // Rate Limit 초과 / AuditLog 이상치 / 밴 처리 플레이어를 하나의 타임라인으로 반환
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? playerId,
        [FromQuery] string? ip)
    {
        // 기간 기본값: 미입력 시 최근 7일
        var fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-7);
        var toUtc   = to?.ToUniversalTime()   ?? DateTime.UtcNow;

        // ① Rate Limit 초과 이벤트
        var rateLimitQuery = _context.RateLimitLogs
            .Where(l => l.OccurredAt >= fromUtc && l.OccurredAt <= toUtc);
        if (playerId.HasValue)
            rateLimitQuery = rateLimitQuery.Where(l => l.PlayerId == playerId);
        if (!string.IsNullOrEmpty(ip))
            rateLimitQuery = rateLimitQuery.Where(l => l.IpAddress == ip);

        var rateLimitRaw = await rateLimitQuery
            .OrderByDescending(l => l.OccurredAt)
            .Take(200)
            .Select(l => new { l.OccurredAt, l.PlayerId, l.IpAddress, l.Path, l.Policy })
            .ToListAsync();

        // ② AuditLog 이상치 이벤트
        var anomalyQuery = _context.AuditLogs
            .Where(l => l.IsAnomaly && l.CreatedAt >= fromUtc && l.CreatedAt <= toUtc);
        if (playerId.HasValue)
            anomalyQuery = anomalyQuery.Where(l => l.PlayerId == playerId);

        var anomalyRaw = await anomalyQuery
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .Select(l => new { l.CreatedAt, l.PlayerId, l.ItemId, l.ChangeAmount, l.Reason })
            .ToListAsync();

        // ③ 밴 처리된 플레이어
        var banQuery = _context.Players
            .IgnoreQueryFilters()
            .Where(p => p.IsBanned);
        if (playerId.HasValue)
            banQuery = banQuery.Where(p => p.Id == playerId);

        var banItems = await banQuery
            .OrderByDescending(p => p.BannedUntil)
            .Take(100)
            .Select(p => new SecurityTimelineItemDto(
                p.BannedUntil ?? DateTime.UtcNow,
                "Ban",
                p.Id,
                null,
                p.BannedUntil == null
                    ? $"영구 정지 — 닉네임: {p.Nickname}"
                    : $"기간 정지 (~ {p.BannedUntil.Value:yyyy-MM-dd HH:mm} UTC) — 닉네임: {p.Nickname}",
                "Critical",
                IsBanned: true))
            .ToListAsync();

        // RateLimit + Anomaly 항목의 PlayerId 배치 조회 — IsBanned 표시용
        var playerIds = rateLimitRaw.Select(l => l.PlayerId)
            .Concat(anomalyRaw.Select(l => (int?)l.PlayerId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var bannedSet = await _context.Players
            .IgnoreQueryFilters()
            .Where(p => playerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsBanned })
            .ToDictionaryAsync(p => p.Id, p => p.IsBanned);

        var rateLimitItems = rateLimitRaw.Select(l => new SecurityTimelineItemDto(
            l.OccurredAt, "RateLimit", l.PlayerId, l.IpAddress,
            $"Rate Limit 초과 — 경로: {l.Path}, 정책: {l.Policy}",
            "Warn",
            IsBanned: l.PlayerId.HasValue && bannedSet.TryGetValue(l.PlayerId.Value, out var b) && b));

        var anomalyItems = anomalyRaw.Select(l => new SecurityTimelineItemDto(
            l.CreatedAt, "Anomaly", l.PlayerId, null,
            $"재화 이상치 — 아이템 ID: {l.ItemId}, 변동: {l.ChangeAmount:+#;-#;0}, 사유: {l.Reason}",
            "Warn",
            IsBanned: bannedSet.TryGetValue(l.PlayerId, out var ab) && ab));

        // 3개 소스 병합 후 시간 내림차순 정렬, 최대 200건
        var timeline = rateLimitItems
            .Concat(anomalyItems)
            .Concat(banItems)
            .OrderByDescending(e => e.OccurredAt)
            .Take(200)
            .ToList();

        return Ok(timeline);
    }
}

// Rate Limit 정책 설정 응답 DTO — appsettings.json 의 RateLimiting 섹션에서 동적으로 읽어 반환
public record RateLimitConfigDto(
    // 미인증(IP) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPermitLimit 키
    int AuthPermitLimit,
    // 인증(PlayerId) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPlayerPermitLimit 키
    int AuthPlayerPermitLimit
);

// 보안 이벤트 통합 DTO — 타입별 이벤트를 단일 형태로 표현
public record SecurityTimelineItemDto(
    DateTime OccurredAt,
    string Type,        // "RateLimit" | "Anomaly" | "Ban"
    int? PlayerId,
    string? IpAddress,
    string Description,
    string Severity,    // "Warn" | "Critical"
    bool IsBanned);     // 해당 PlayerId의 현재 밴 여부
