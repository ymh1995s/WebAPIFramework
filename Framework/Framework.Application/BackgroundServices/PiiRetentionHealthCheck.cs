using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Framework.Application.BackgroundServices;

// PII 보관기간 정리 서비스 상태를 /health 엔드포인트에 노출하는 헬스체크
// PiiRetentionHealthState(Singleton)에서 마지막 실행 결과를 읽어 판정
// 판정 기준:
//   - Healthy   : LastSuccessUtc가 HealthyThresholdHours 이내
//   - Degraded  : LastSuccessUtc가 HealthyThresholdHours 초과 ~ UnhealthyThresholdHours 이내
//   - Unhealthy : LastSuccessUtc가 없거나 UnhealthyThresholdHours 초과
public sealed class PiiRetentionHealthCheck : IHealthCheck
{
    // 헬스 상태를 읽어올 공유 상태 객체 (Singleton)
    private readonly PiiRetentionHealthState _state;

    // 임계값 설정 — PiiRetentionOptions에서 주입
    private readonly IOptions<PiiRetentionOptions> _options;

    // 시간 소스 — 단위 테스트에서 TimeProvider.Fixed()로 교체 가능
    private readonly TimeProvider _timeProvider;

    public PiiRetentionHealthCheck(
        PiiRetentionHealthState state,
        IOptions<PiiRetentionOptions> options,
        TimeProvider timeProvider)
    {
        _state = state;
        _options = options;
        _timeProvider = timeProvider;
    }

    // 헬스 상태 판정 — /health 호출 시 ASP.NET Core 헬스체크 프레임워크가 자동 호출
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // 아직 한 번도 성공하지 않은 경우 — 서버 방금 기동 또는 누적 실패
        if (_state.LastSuccessUtc is null)
        {
            var description = _state.LastFailureMessage is not null
                ? $"성공 이력 없음. 마지막 실패: {_state.LastFailureMessage}"
                : "성공 이력 없음 (아직 실행 전)";

            return Task.FromResult(HealthCheckResult.Unhealthy(description));
        }

        // 마지막 성공으로부터 경과 시간 계산
        var elapsed = now - _state.LastSuccessUtc.Value;

        if (elapsed.TotalHours <= opts.HealthyThresholdHours)
        {
            // 정상 범위 — 최근 성공 이력 있음
            return Task.FromResult(HealthCheckResult.Healthy(
                $"마지막 성공: {_state.LastSuccessUtc:yyyy-MM-dd HH:mm} UTC ({elapsed.TotalHours:0.0}h 전)"));
        }

        if (elapsed.TotalHours <= opts.UnhealthyThresholdHours)
        {
            // 경고 범위 — 예정보다 늦어지고 있음
            return Task.FromResult(HealthCheckResult.Degraded(
                $"마지막 성공: {_state.LastSuccessUtc:yyyy-MM-dd HH:mm} UTC ({elapsed.TotalHours:0.0}h 전) — 지연 감지"));
        }

        // 비정상 범위 — 장기간 실행 실패
        var failureInfo = _state.LastFailureMessage is not null
            ? $" 마지막 실패: {_state.LastFailureMessage}"
            : string.Empty;

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"마지막 성공: {_state.LastSuccessUtc:yyyy-MM-dd HH:mm} UTC ({elapsed.TotalHours:0.0}h 전) — 임계값 초과.{failureInfo}"));
    }
}
