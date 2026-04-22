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

    // 구글 서버에 IdToken 검증 요청 후 GoogleId(Subject) 반환
    public async Task<string> VerifyAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            // appsettings.json의 ClientId와 일치하는지 검증
            Audience = new[] { _config["Google:ClientId"] }
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            // Subject = 구글 고유 사용자 ID
            return payload.Subject;
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("유효하지 않은 구글 토큰입니다.");
        }
    }
}
