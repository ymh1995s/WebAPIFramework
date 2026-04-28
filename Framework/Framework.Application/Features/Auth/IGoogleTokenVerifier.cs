namespace Framework.Application.Features.Auth;

// 구글 IdToken 검증 인터페이스
public interface IGoogleTokenVerifier
{
    // 구글 IdToken을 검증하고 GoogleId(Subject) 반환
    Task<string> VerifyAsync(string idToken);
}
