namespace Framework.Application.Features.Auth.Exceptions;

// 구글 IdToken 검증 요청이 설정된 타임아웃 시간 내에 완료되지 않았을 때 발생하는 예외.
// 클라이언트에게는 503 Service Unavailable로 노출되어 재시도를 유도한다.
public class GoogleTokenVerificationTimeoutException : AuthDomainException
{
    public override string ErrorCode => "AUTH_GOOGLE_VERIFY_TIMEOUT";

    public GoogleTokenVerificationTimeoutException(int timeoutSeconds)
        : base($"구글 토큰 검증 서버 응답 대기 시간({timeoutSeconds}초)을 초과했습니다.")
    {
    }
}
