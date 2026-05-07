namespace Framework.Application.Features.Auth.Exceptions;

// 유효하지 않은 리프래시 토큰 (DB에 존재하지 않음)
public class InvalidRefreshTokenException : AuthDomainException
{
    public override string ErrorCode => "AUTH_TOKEN_INVALID";
    public InvalidRefreshTokenException(string message = "유효하지 않은 리프래시 토큰입니다.") : base(message) { }
}
