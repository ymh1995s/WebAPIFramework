using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// Rate Limit 로그 저장소 인터페이스 — M-5/L-23 해소
// 기존 직접 구현체 참조를 인터페이스 의존으로 전환하기 위해 분리
public interface IRateLimitLogRepository
{
    // OnRejected 콜백에서 사용 — 변경 추적만 등록 (실제 저장은 호출자 SaveChangesAsync)
    Task AddAsync(RateLimitLog log);

    // 변경 사항 저장 — 호출자가 저장 시점을 명시적으로 제어
    Task SaveChangesAsync();

    // IP별 집계 — Admin GET /api/admin/rate-limit-logs (Count 내림차순 정렬)
    Task<List<(string IpAddress, int Count, DateTime LastOccurredAt)>> GetAggregatedByIpAsync();

    // 기간 + 선택 필터(playerId/ip)로 최근 N건 조회 — Admin Timeline (OccurredAt 내림차순)
    Task<List<RateLimitLog>> GetRecentByFiltersAsync(
        DateTime fromUtc, DateTime toUtc, int? playerId, string? ipAddress, int take);
}
