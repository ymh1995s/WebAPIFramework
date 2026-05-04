namespace Framework.Application.Features.Security;

// 보안 화면 통합 서비스 인터페이스
// RateLimit 집계 + 다중 도메인 타임라인(RateLimitLog / AuditLog 이상치 / 밴)을 하나의 서비스로 통합
// H-1(AppDbContext 직접 주입 제거) 해소를 위해 컨트롤러 → 서비스 레이어로 이전
public interface ISecurityAdminService
{
    // IP별 Rate Limit 집계 조회 — Admin GET /api/admin/rate-limit-logs
    Task<List<RateLimitIpAggregateDto>> GetRateLimitAggregatedByIpAsync();

    // 보안 통합 타임라인 조회 — RateLimitLog + AuditLog 이상치 + 밴 플레이어를 하나의 흐름으로 조합
    // from/to null 시 서비스 내부에서 최근 7일 기본값 적용 (UTC 변환은 컨트롤러에서 처리 후 전달)
    Task<List<SecurityTimelineItemDto>> GetTimelineAsync(
        DateTime? from, DateTime? to, int? playerId, string? ip);
}
