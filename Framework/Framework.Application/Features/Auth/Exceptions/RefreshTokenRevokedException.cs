namespace Framework.Application.Features.Auth.Exceptions;

// 폐기된 리프래시 토큰 (강제 로그아웃 등)
public class RefreshTokenRevokedException : AuthDomainException
{
    public override string ErrorCode => "AUTH_TOKEN_REVOKED";
    public RefreshTokenRevokedException(string message = "폐기된 리프래시 토큰입니다.") : base(message) { }
}
