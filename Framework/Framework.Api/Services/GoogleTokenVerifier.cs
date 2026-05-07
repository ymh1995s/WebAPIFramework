using Framework.Application.Features.Auth;
using Framework.Application.Features.Auth.Exceptions;
using Google.Apis.Auth;

namespace Framework.Api.Services;

// 구글 IdToken 검증 구현체 - Google.Apis.Auth 라이브러리 사용
public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly IConfiguration _config;

    public GoogleTokenVerifier(IConfiguration config)
    {
        _config = config;
    }

    // 구글 서버에 IdToken 검증 요청 후 GoogleId(Subject) 반환.
    // appsettings의 Google:VerifyTimeoutSeconds 이내에 응답이 없으면 503 반환을 위한 예외를 던진다.
    public async Task<string> VerifyAsync(string idToken)
    {
        // 타임아웃 설정값 읽기 — 미설정 시 기본값 5초 사용
        var timeoutSeconds = _config.GetValue<int>("Google:VerifyTimeoutSeconds", 5);

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            // appsettings.json의 ClientId와 일치하는지 검증
            Audience = new[] { _config["Google:ClientId"] }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // WaitAsync로 타임아웃 제어 — ValidateAsync 자체는 CancellationToken을 지원하지 않으므로 래핑
            var payload = await GoogleJsonWebSignature
                .ValidateAsync(idToken, settings)
                .WaitAsync(cts.Token);

            // Subject = 구글 고유 사용자 ID
            return payload.Subject;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // 타임아웃 초과 — 구글 검증 서버 응답 지연으로 서비스 불가 상태
            throw new GoogleTokenVerificationTimeoutException(timeoutSeconds);
        }
        catch (InvalidJwtException)
        {
            throw new InvalidGoogleTokenException();
        }
    }
}
