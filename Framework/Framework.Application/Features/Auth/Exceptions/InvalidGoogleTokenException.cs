namespace Framework.Application.Features.Auth.Exceptions;

// 유효하지 않은 구글 IdToken — GoogleTokenVerifier 검증 실패 시 발생
public class InvalidGoogleTokenException : AuthDomainException
{
    public override string ErrorCode => "AUTH_GOOGLE_TOKEN_INVALID";
    public InvalidGoogleTokenException(string message = "유효하지 않은 구글 토큰입니다.") : base(message) { }
}
