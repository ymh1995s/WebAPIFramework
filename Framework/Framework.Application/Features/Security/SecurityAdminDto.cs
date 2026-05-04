namespace Framework.Application.Features.Security;

// IP별 Rate Limit 집계 응답 DTO — Admin GET /api/admin/rate-limit-logs 응답 매핑
// Razor 측 RateLimitLogDto(IpAddress, Count, LastOccurredAt)와 필드명·타입·순서 일치
public record RateLimitIpAggregateDto(string IpAddress, int Count, DateTime LastOccurredAt);

// 보안 통합 Timeline 항목 DTO
// 기존 AdminSecurityController.cs 하단의 SecurityTimelineItemDto를 Application 레이어로 이동
// Razor 측 SecurityTimelineItemDto 와 필드명·타입·순서 완전 일치 — 스키마 호환성 필수
public record SecurityTimelineItemDto(
    DateTime OccurredAt,
    string Type,         // "RateLimit" / "Anomaly" / "Ban"
    int? PlayerId,
    string? IpAddress,
    string Description,
    string Severity,     // "Warn" / "Critical"
    bool IsBanned);      // 해당 PlayerId의 현재 밴 여부
