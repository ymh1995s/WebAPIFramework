using Framework.Application.Common;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Framework.Api.Extensions;

// 외부 API 복원력 파이프라인 등록 확장 메서드
// Polly ResiliencePipeline 3종을 DI에 등록한다:
//   "external-iap-verify"  — Google Play 영수증 검증
//   "external-iap-consume" — Google Play consume
//   "external-google-auth" — Google OAuth 토큰 검증
public static class ResilienceExtensions
{
    // Polly 파이프라인 키 상수 — 주입 시 문자열 키 오타 방지
    public const string IapVerifyKey = "external-iap-verify";
    public const string IapConsumeKey = "external-iap-consume";
    public const string GoogleAuthKey = "external-google-auth";

    // appsettings.json "ExternalApi" 섹션을 읽어 복원력 파이프라인 3종을 등록
    public static IServiceCollection AddResilience(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ExternalApiOptions를 IOptions<T> 패턴으로 등록 — 재배포 필요, PiiRetention과 동일 정책(D-6)
        services.Configure<ExternalApiOptions>(configuration.GetSection("ExternalApi"));

        // ResiliencePipelineProvider<string> 를 사용하기 위해 Polly DI 확장 등록
        services.AddResiliencePipeline<string>(IapVerifyKey, (builder, context) =>
        {
            var opts = context.ServiceProvider
                .GetRequiredService<IOptions<ExternalApiOptions>>().Value;

            // 비상 정지 스위치: Enabled=false면 no-op 파이프라인 (콜백 직통 통과)
            if (!opts.Enabled) return;

            BuildPipeline(builder, opts.IapVerify);
        });

        services.AddResiliencePipeline<string>(IapConsumeKey, (builder, context) =>
        {
            var opts = context.ServiceProvider
                .GetRequiredService<IOptions<ExternalApiOptions>>().Value;

            if (!opts.Enabled) return;

            BuildPipeline(builder, opts.IapConsume);
        });

        services.AddResiliencePipeline<string>(GoogleAuthKey, (builder, context) =>
        {
            var opts = context.ServiceProvider
                .GetRequiredService<IOptions<ExternalApiOptions>>().Value;

            if (!opts.Enabled) return;

            BuildPipeline(builder, opts.GoogleAuth);
        });

        return services;
    }

    // 개별 파이프라인 구성 — 타임아웃 → 재시도 → 서킷브레이커 순서로 중첩
    // Polly v8 파이프라인 실행 순서(바깥→안):
    //   타임아웃이 가장 바깥 → 재시도 → 서킷브레이커 → 실제 호출
    // 의미: 타임아웃은 재시도 포함 전체에 적용. 서킷 OPEN 시 재시도·타임아웃 소비 없이 즉시 차단.
    private static void BuildPipeline(
        ResiliencePipelineBuilder builder,
        ExternalApiPipelineOptions opts)
    {
        // 1) 타임아웃 — 재시도 포함 전체 호출에 대한 상위 타임아웃
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds)
        });

        // 2) 재시도 — RetryCount > 0인 경우만 추가 (IAP verify·Google auth는 0회)
        if (opts.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(
                    // TimeoutRejectedException은 재시도 대상에서 제외 — 타임아웃 후 재시도는 무의미
                    ex => ex is not TimeoutRejectedException),
                MaxRetryAttempts = opts.RetryCount,
                // 지수 백오프: 1회=RetryBaseDelayMs, 2회≈4*RetryBaseDelayMs
                Delay = TimeSpan.FromMilliseconds(opts.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false
            });
        }

        // 3) 서킷브레이커 — 샘플링 윈도우 내 임계 횟수 이상 실패 시 OPEN
        // Polly v8 비율 기반 서킷브레이커를 카운트 임계값처럼 사용:
        //   MinimumThroughput = 임계 횟수, FailureRatio = 1.0(모든 호출이 실패면 개방)
        // 설계 §4: "30초 내 5회 실패 시 OPEN" → MinimumThroughput=5, FailureRatio=1.0
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(
                // BrokenCircuitException 자체는 서킷 카운터에 포함시키지 않음
                ex => ex is not BrokenCircuitException and not IsolatedCircuitException),

            // 샘플링 윈도우 — 이 기간 내 발생한 호출을 기준으로 실패율 계산
            SamplingDuration = TimeSpan.FromSeconds(opts.CircuitSamplingDurationSeconds),

            // 최소 처리량 = 임계 횟수: N회 이상 호출이 있을 때만 서킷 판정 수행
            MinimumThroughput = opts.CircuitFailureThreshold,

            // 실패율 임계값: 1.0 = 모든 호출이 실패인 경우 OPEN (카운트 기반 의미)
            FailureRatio = 1.0,

            // OPEN 유지 시간 — 이 시간 후 Half-Open(probe 1회 허용)으로 전환
            BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakDurationSeconds),
        });
    }
}
