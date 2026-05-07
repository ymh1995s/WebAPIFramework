namespace Framework.Application.Features.Auth.Exceptions;

// 만료된 리프래시 토큰
public class RefreshTokenExpiredException : AuthDomainException
{
    public override string ErrorCode => "AUTH_TOKEN_EXPIRED";
    public RefreshTokenExpiredException(string message = "리프래시 토큰이 만료되었습니다.") : base(message) { }
}
