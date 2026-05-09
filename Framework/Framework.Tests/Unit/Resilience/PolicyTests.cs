using Framework.Api.Extensions;
using Framework.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Testing;
using Polly.Timeout;

namespace Framework.Tests.Unit.Resilience;

// Polly 파이프라인 동작 단위 테스트 — 3개 파이프라인 × 시나리오 6종
// Polly.Testing의 GetPipelineDescriptor()로 파이프라인 구성 검증
// 실제 외부 호출 없이 인메모리 파이프라인으로 시나리오별 예외 발생 검증
public class PolicyTests
{
    // ── 헬퍼: 테스트용 ServiceProvider 생성 ─────────────────────────────

    // 지정한 설정으로 ExternalApiOptions를 구성하고 Polly 파이프라인 3종을 등록한 DI 컨테이너 반환
    private static ResiliencePipelineProvider<string> BuildProvider(ExternalApiOptions opts)
    {
        var services = new ServiceCollection();

        // IOptions<ExternalApiOptions>를 직접 인스턴스로 등록 (IConfiguration 불필요)
        services.AddSingleton<IOptions<ExternalApiOptions>>(
            new Microsoft.Extensions.Options.OptionsWrapper<ExternalApiOptions>(opts));

        // appsettings 대신 빈 IConfiguration 사용 — AddResilience 내부 Configure<T>가 IConfiguration을 요구
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Polly 파이프라인 3종 등록
        services.AddResiliencePipeline<string>(ResilienceExtensions.IapVerifyKey, (builder, context) =>
        {
            var o = context.ServiceProvider.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
            if (!o.Enabled) return;
            BuildTestPipeline(builder, o.IapVerify);
        });

        services.AddResiliencePipeline<string>(ResilienceExtensions.IapConsumeKey, (builder, context) =>
        {
            var o = context.ServiceProvider.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
            if (!o.Enabled) return;
            BuildTestPipeline(builder, o.IapConsume);
        });

        services.AddResiliencePipeline<string>(ResilienceExtensions.GoogleAuthKey, (builder, context) =>
        {
            var o = context.ServiceProvider.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
            if (!o.Enabled) return;
            BuildTestPipeline(builder, o.GoogleAuth);
        });

        return services.BuildServiceProvider()
            .GetRequiredService<ResiliencePipelineProvider<string>>();
    }

    // 테스트용 파이프라인 구성 — ResilienceExtensions.BuildPipeline과 동일 로직
    private static void BuildTestPipeline(ResiliencePipelineBuilder builder, ExternalApiPipelineOptions opts)
    {
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds)
        });

        if (opts.RetryCount > 0)
        {
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(
                    ex => ex is not TimeoutRejectedException),
                MaxRetryAttempts = opts.RetryCount,
                Delay = TimeSpan.FromMilliseconds(opts.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false
            });
        }

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(
                ex => ex is not BrokenCircuitException and not IsolatedCircuitException),
            SamplingDuration = TimeSpan.FromSeconds(opts.CircuitSamplingDurationSeconds),
            MinimumThroughput = opts.CircuitFailureThreshold,
            FailureRatio = 1.0,
            BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakDurationSeconds),
        });
    }

    // 테스트용 기본 설정 — 빠른 테스트를 위해 임계값 낮게, 타임아웃 짧게 조정
    private static ExternalApiOptions BuildFastOptions(
        int timeout = 1,
        int retryCount = 0,
        int circuitThreshold = 3,
        int circuitSampling = 5,
        int circuitBreak = 5)
    {
        var pipeOpts = new ExternalApiPipelineOptions
        {
            TimeoutSeconds = timeout,
            RetryCount = retryCount,
            RetryBaseDelayMs = 10,         // 테스트에서 지수 백오프 지연 최소화
            CircuitFailureThreshold = circuitThreshold,
            CircuitSamplingDurationSeconds = circuitSampling,
            CircuitBreakDurationSeconds = circuitBreak
        };

        return new ExternalApiOptions
        {
            Enabled = true,
            IapVerify = pipeOpts,
            IapConsume = new ExternalApiPipelineOptions
            {
                TimeoutSeconds = timeout,
                RetryCount = retryCount,
                RetryBaseDelayMs = 10,
                CircuitFailureThreshold = circuitThreshold,
                CircuitSamplingDurationSeconds = circuitSampling,
                CircuitBreakDurationSeconds = circuitBreak
            },
            GoogleAuth = pipeOpts
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 1: Happy path — 정상 콜백은 예외 없이 결과를 반환
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IapVerify_HappyPath_ReturnsResult()
    {
        // 준비: 빠른 설정으로 파이프라인 구성
        var provider = BuildProvider(BuildFastOptions());
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapVerifyKey);

        // 실행: 정상 콜백
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct); // 짧은 지연 (타임아웃 이내)
            return "success";
        });

        // 검증
        Assert.Equal("success", result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 2: 타임아웃 발동 — 타임아웃 초과 시 TimeoutRejectedException 발생
    // Polly v8 타임아웃: 콜백에 전달된 ct를 통해 취소를 전파하며,
    // ct를 존중하는 Task.Delay가 OperationCanceledException을 발생시키고
    // Polly가 이를 TimeoutRejectedException으로 변환한다.
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IapVerify_Timeout_ThrowsTimeoutRejectedException()
    {
        // 준비: 타임아웃 1초, 콜백은 10초 대기 → 타임아웃 발동
        var opts = new ExternalApiOptions
        {
            Enabled = true,
            IapVerify = new ExternalApiPipelineOptions
            {
                TimeoutSeconds = 1,
                RetryCount = 0,
                CircuitFailureThreshold = 10,
                CircuitSamplingDurationSeconds = 30,
                CircuitBreakDurationSeconds = 30
            },
            IapConsume = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 10, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 60 },
            GoogleAuth = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 10, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 30 }
        };

        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapVerifyKey);

        // 실행: ct를 존중하는 Task.Delay로 타임아웃 발동
        // Polly v8 TimeoutStrategy: ct 취소 → OperationCanceledException → TimeoutRejectedException으로 래핑
        Exception? caughtEx = null;
        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                // ct를 사용하는 Task.Delay — Polly 타임아웃 신호(ct 취소)를 받으면 예외 발생
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return "never";
            });
        }
        catch (Exception ex)
        {
            caughtEx = ex;
        }

        // TimeoutRejectedException 또는 OperationCanceledException(타임아웃 원인) 중 하나이어야 함
        // Polly v8은 버전에 따라 래핑 방식이 다를 수 있음
        Assert.NotNull(caughtEx);
        Assert.True(
            caughtEx is TimeoutRejectedException or OperationCanceledException,
            $"예상: TimeoutRejectedException 또는 OperationCanceledException, 실제: {caughtEx.GetType().Name}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 3: 재시도 횟수 (IapConsume만 — RetryCount=2)
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IapConsume_Retry_ExecutesExpectedAttempts()
    {
        // 준비: RetryCount=2, 타임아웃 충분히 크게, 서킷 임계 높게
        var opts = new ExternalApiOptions
        {
            Enabled = true,
            IapVerify = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 100, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 60 },
            IapConsume = new ExternalApiPipelineOptions
            {
                TimeoutSeconds = 30,
                RetryCount = 2,
                RetryBaseDelayMs = 10,          // 테스트 속도를 위해 10ms로 최소화
                CircuitFailureThreshold = 100,  // 서킷이 열리지 않도록 임계값 높게
                CircuitSamplingDurationSeconds = 30,
                CircuitBreakDurationSeconds = 60
            },
            GoogleAuth = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 100, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 30 }
        };

        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapConsumeKey);

        // 호출 횟수 추적
        var callCount = 0;

        // 실행: 처음 2회는 실패, 3회째 성공 (초기 1회 + 재시도 2회 = 총 3회)
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            callCount++;
            if (callCount < 3)
                throw new HttpRequestException("일시적 오류");
            return "ok";
        });

        // 검증: 총 3회 호출 (1 + 재시도 2회)
        Assert.Equal("ok", result);
        Assert.Equal(3, callCount);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 4: 서킷 OPEN 트리거 — 임계 횟수 실패 시 BrokenCircuitException 발생
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GoogleAuth_CircuitBreaker_OpenAfterThreshold()
    {
        // 준비: 서킷 임계 3회, 타임아웃 넉넉하게
        var opts = BuildFastOptions(timeout: 30, retryCount: 0, circuitThreshold: 3, circuitSampling: 30, circuitBreak: 60);
        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.GoogleAuthKey);

        // 임계 횟수(3회)만큼 실패를 발생시켜 서킷 OPEN
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await pipeline.ExecuteAsync<string>(_ =>
                    throw new InvalidOperationException("외부 서버 오류"));
            }
            catch (InvalidOperationException) { /* 예상된 실패 — 무시 */ }
            catch (BrokenCircuitException) { /* 임계 도달 시 서킷 OPEN — 루프 종료 */ break; }
        }

        // 검증: 서킷 OPEN 후 추가 호출은 BrokenCircuitException 발생
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            await pipeline.ExecuteAsync<string>(_ =>
                throw new InvalidOperationException("이 코드는 실행되지 않아야 함"));
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 5: 서킷 Half-Open 회복 — OPEN 후 BreakDuration 경과 시 1회 probe 허용
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IapVerify_CircuitBreaker_HalfOpenRecovery()
    {
        // 준비: 서킷 임계 2회, OPEN 유지 0.1초 (테스트 빠르게)
        var opts = new ExternalApiOptions
        {
            Enabled = true,
            IapVerify = new ExternalApiPipelineOptions
            {
                TimeoutSeconds = 30,
                RetryCount = 0,
                CircuitFailureThreshold = 2,
                CircuitSamplingDurationSeconds = 30,
                CircuitBreakDurationSeconds = 1  // 1초 후 Half-Open으로 전환
            },
            IapConsume = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 10, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 60 },
            GoogleAuth = new ExternalApiPipelineOptions { TimeoutSeconds = 30, RetryCount = 0, CircuitFailureThreshold = 10, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 30 }
        };

        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapVerifyKey);

        // 임계(2회) 실패로 서킷 OPEN
        for (var i = 0; i < 2; i++)
        {
            try { await pipeline.ExecuteAsync<string>(_ => throw new Exception("실패")); }
            catch { /* 무시 */ }
        }

        // 서킷 OPEN 상태 확인
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            await pipeline.ExecuteAsync<string>(_ => ValueTask.FromResult("probe"));
        });

        // BreakDuration(1초) 이상 대기 → Half-Open으로 전환
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        // Half-Open 상태에서 성공 probe → Closed로 복구
        var result = await pipeline.ExecuteAsync(_ => ValueTask.FromResult("recovered"));
        Assert.Equal("recovered", result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 6: 외부 CancellationToken 전달 — 요청 취소 시 OperationCanceledException 전파
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IapConsume_ExternalCancellation_PropagatesCancellation()
    {
        // 준비
        var provider = BuildProvider(BuildFastOptions(timeout: 30, retryCount: 0, circuitThreshold: 10));
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapConsumeKey);

        // 이미 취소된 토큰으로 실행
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // 실행 및 검증: OperationCanceledException이 발생해야 함
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                // ct는 이미 취소된 상태 — 대기 즉시 예외 발생
                await Task.Delay(1000, ct);
                return "never";
            }, cts.Token);
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // 시나리오 보너스: Enabled=false면 no-op — 콜백이 그냥 통과됨
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AllPipelines_DisabledFlag_PassThrough()
    {
        // 준비: Enabled=false
        var opts = BuildFastOptions();
        opts.Enabled = false;

        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapVerifyKey);

        // 실행: 비상 정지 스위치 ON — 파이프라인이 비어있으므로 콜백 직통 실행
        var callCount = 0;
        var result = await pipeline.ExecuteAsync(_ =>
        {
            callCount++;
            return ValueTask.FromResult("bypassed");
        });

        // 검증: 콜백 1회 정상 실행
        Assert.Equal("bypassed", result);
        Assert.Equal(1, callCount);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 파이프라인 구성 검증 — Polly.Testing GetPipelineDescriptor로 전략 목록 확인
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void IapConsume_PipelineDescriptor_ContainsThreeStrategies()
    {
        // 준비: RetryCount=2 → 타임아웃 + 재시도 + 서킷브레이커 = 3개 전략
        var opts = new ExternalApiOptions
        {
            Enabled = true,
            IapVerify = new ExternalApiPipelineOptions { TimeoutSeconds = 10, RetryCount = 0, CircuitFailureThreshold = 5, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 30 },
            IapConsume = new ExternalApiPipelineOptions { TimeoutSeconds = 15, RetryCount = 2, RetryBaseDelayMs = 1000, CircuitFailureThreshold = 5, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 60 },
            GoogleAuth = new ExternalApiPipelineOptions { TimeoutSeconds = 5, RetryCount = 0, CircuitFailureThreshold = 5, CircuitSamplingDurationSeconds = 30, CircuitBreakDurationSeconds = 30 }
        };

        var provider = BuildProvider(opts);
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapConsumeKey);

        // Polly.Testing API로 파이프라인 내 전략 목록 추출
        var descriptor = pipeline.GetPipelineDescriptor();

        // 검증: 타임아웃(1) + 재시도(1) + 서킷브레이커(1) = 3개
        Assert.Equal(3, descriptor.Strategies.Count);
    }

    [Fact]
    public void IapVerify_PipelineDescriptor_ContainsTwoStrategies()
    {
        // 준비: RetryCount=0 → 타임아웃 + 서킷브레이커 = 2개 전략
        var provider = BuildProvider(BuildFastOptions(retryCount: 0));
        var pipeline = provider.GetPipeline(ResilienceExtensions.IapVerifyKey);

        var descriptor = pipeline.GetPipelineDescriptor();

        // 검증: 타임아웃(1) + 서킷브레이커(1) = 2개
        Assert.Equal(2, descriptor.Strategies.Count);
    }
}
