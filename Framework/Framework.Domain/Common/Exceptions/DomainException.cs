namespace Framework.Domain.Common.Exceptions;

// 모든 도메인·애플리케이션 예외의 루트 베이스.
// .NET 표준 예외와 분리하여 글로벌 핸들러 광역 catch 방지.
// ErrorCode는 Unity 클라이언트가 분기 처리하는 안정 식별자 (형식: AUTH_BANNED).
public abstract class DomainException : Exception
{
    // Unity 클라이언트 분기용 에러 코드 — 대문자_언더스코어 형식
    public abstract string ErrorCode { get; }

    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception? inner) : base(message, inner) { }
}
