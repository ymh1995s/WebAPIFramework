namespace Framework.Application.Features.Auth.Exceptions;

// 정지(밴) 상태 계정의 인증 시도 — GuestLogin 컨트롤러는 이 예외만 캐치하여 403 반환
public class PlayerBannedException : AuthDomainException
{
    public override string ErrorCode => "AUTH_BANNED";
    public PlayerBannedException(string message = "정지된 계정입니다.") : base(message) { }
}
