namespace Framework.Admin.Components.Shared;

// API 헬스체크 응답 — /health 엔드포인트 본문 전체 매핑
public record HealthResponse(
    string Status,
    double TotalDuration,
    HealthCheckEntry[] Checks);

// 의존성별 헬스체크 항목 — DB, 캐시 등 각 체크 결과
public record HealthCheckEntry(
    string Name,
    string Status,
    string? Description,
    double Duration);

// 헤더 점 색상 분류 — Loading/Healthy/Degraded/Unhealthy/Offline 5종
internal enum HealthDotState { Loading, Healthy, Degraded, Unhealthy, Offline }
