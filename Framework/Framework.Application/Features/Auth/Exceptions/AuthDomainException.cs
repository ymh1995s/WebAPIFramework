namespace Framework.Application.Features.Auth.Exceptions;

// 인증 도메인 공통 베이스 예외 — 핸들러는 이 타입만 캐치하면 모든 인증 실패를 처리할 수 있다.
// .NET의 UnauthorizedAccessException(파일·권한 등)과 의도적으로 분리하여 우연한 광역 캐치를 방지한다.
public abstract class AuthDomainException : Exception
{
    // ProblemDetails 응답의 errorCode 필드에 들어갈 값 (Unity 클라이언트 분기 처리에 사용)
    public abstract string ErrorCode { get; }
    protected AuthDomainException(string message) : base(message) { }
}
