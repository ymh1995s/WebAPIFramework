namespace Framework.Api.Constants;

// Admin 페이지네이션 PageSize 상한 — 비정상적으로 큰 PageSize 요청 차단(M-37 후속 정리)
public static class PaginationLimits
{
    // Admin 일반 목록 — 100건 (Players, Shouts, BanLogs, IapProducts, Stages)
    public const int AdminDefault = 100;

    // Admin 알림 — 200건 (30초 폴링 대량 누적 처리)
    public const int AdminNotifications = 200;

    // Admin 대용량 분석 로그 — 500건 (AuditLog/RateLimitLog CSV 분석)
    public const int AdminLargeLog = 500;
}
