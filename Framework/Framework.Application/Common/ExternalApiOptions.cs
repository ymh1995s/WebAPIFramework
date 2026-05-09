namespace Framework.Application.Common;

// 외부 API 복원력 정책 설정 — appsettings.json "ExternalApi" 섹션에 바인딩되는 POCO
// IOptions<ExternalApiOptions>로 주입하여 각 파이프라인 등록에 사용
public sealed class ExternalApiOptions
{
    // 비상 정지 스위치: false로 설정하면 Polly 파이프라인이 no-op(콜백 직통)으로 동작
    // 외부 API 전면 장애 시 서킷브레이커 대기 없이 즉시 요청을 통과시킬 수 있음
    public bool Enabled { get; set; } = true;

    // Google Play IAP 검증 파이프라인 설정
    public ExternalApiPipelineOptions IapVerify { get; set; } = new();

    // Google Play IAP consume 파이프라인 설정
    public ExternalApiPipelineOptions IapConsume { get; set; } = new();

    // Google OAuth 토큰 검증 파이프라인 설정
    public ExternalApiPipelineOptions GoogleAuth { get; set; } = new();
}

// 개별 파이프라인 타임아웃·재시도·서킷브레이커 설정값
public sealed class ExternalApiPipelineOptions
{
    // 단일 호출 타임아웃 (초)
    public int TimeoutSeconds { get; set; } = 10;

    // 재시도 횟수 (0이면 재시도 없음)
    public int RetryCount { get; set; } = 0;

    // 재시도 기본 지연 (밀리초) — 지수 백오프 기준값: 1회=BaseDelayMs, 2회=4*BaseDelayMs
    public int RetryBaseDelayMs { get; set; } = 1000;

    // 서킷브레이커 — CircuitSamplingDurationSeconds 구간 내 연속 실패 임계 횟수
    public int CircuitFailureThreshold { get; set; } = 5;

    // 서킷브레이커 샘플링 윈도우 (초)
    public int CircuitSamplingDurationSeconds { get; set; } = 30;

    // 서킷 OPEN 유지 시간 (초) — 이 시간이 지난 뒤 Half-Open으로 전환
    public int CircuitBreakDurationSeconds { get; set; } = 30;
}
