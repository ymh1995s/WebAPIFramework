using Framework.Api.Extensions;
using Framework.Application.Features.Auth;
using Framework.Application.Features.Auth.Exceptions;
using Google.Apis.Auth;
using Polly.Registry;

namespace Framework.Api.Services;

// 구글 IdToken 검증 구현체 - Google.Apis.Auth 라이브러리 사용
// Polly "external-google-auth" 파이프라인이 타임아웃(5s)·서킷브레이커를 담당
// 기존 자체 CancellationTokenSource 래핑은 Polly로 대체됨
public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly IConfiguration _config;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public GoogleTokenVerifier(
        IConfiguration config,
        ResiliencePipelineProvider<string> pipelineProvider)
    {
        _config = config;
        _pipelineProvider = pipelineProvider;
    }

    // 구글 서버에 IdToken 검증 요청 후 GoogleId(Subject) 반환.
    // Polly "external-google-auth" 파이프라인이 5초 타임아웃·서킷브레이커를 적용.
    // 타임아웃 초과 시 GoogleTokenVerificationTimeoutException 발생 (AuthDomainExceptionHandler에서 503 처리).
    public async Task<string> VerifyAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            // appsettings.json의 ClientId와 일치하는지 검증
            Audience = new[] { _config["Google:ClientId"] }
        };

        // Polly 파이프라인 로드 — 타임아웃·서킷브레이커 적용
        var pipeline = _pipelineProvider.GetPipeline(ResilienceExtensions.GoogleAuthKey);

        try
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                // Google SDK ValidateAsync는 CancellationToken 미지원 → WaitAsync로 Polly ct 연결
                var payload = await GoogleJsonWebSignature
                    .ValidateAsync(idToken, settings)
                    .WaitAsync(ct);

                // Subject = 구글 고유 사용자 ID
                return payload.Subject;
            });
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            // Polly 타임아웃 초과 — 기존과 동일한 예외 타입으로 변환하여 AuthDomainExceptionHandler에서 처리
            // 타임아웃 설정값을 로그에 포함 (디버깅 편의)
            var timeoutSeconds = _config.GetValue<int>("ExternalApi:GoogleAuth:TimeoutSeconds", 5);
            throw new GoogleTokenVerificationTimeoutException(timeoutSeconds);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            // 서킷브레이커 OPEN — Google 인증 서버 연속 장애 상태
            // 타임아웃과 동일한 예외 타입으로 변환하여 503 반환 경로 통일
            var timeoutSeconds = _config.GetValue<int>("ExternalApi:GoogleAuth:TimeoutSeconds", 5);
            throw new GoogleTokenVerificationTimeoutException(timeoutSeconds);
        }
        catch (InvalidJwtException)
        {
            // JWT 자체 검증 실패 — 토큰 위변조 또는 만료
            throw new InvalidGoogleTokenException();
        }
        // 그 외 예외(네트워크 오류 등)는 GlobalExceptionHandler로 전파
    }
}
