using Framework.Domain.Common.Exceptions;

namespace Framework.Application.Features.Iap.Exceptions;

// IAP 외부 서비스 일시 불가 예외 — 서킷브레이커 OPEN 또는 타임아웃으로 인한 인프라 장애
// BrokenCircuitException / TimeoutRejectedException을 컨트롤러에서 이 예외로 변환하여 503 매핑
public sealed class IapServiceUnavailableException : DomainException
{
    // Unity 클라이언트가 ErrorCode로 분기 처리할 키
    public override string ErrorCode => "IAP_VERIFY_UNAVAILABLE";

    // 장애 원인 분류 — 타임아웃과 서킷 OPEN을 클라이언트가 구분할 수 있도록 제공
    public IapUnavailableReason Reason { get; }

    public IapServiceUnavailableException(IapUnavailableReason reason)
        : base(GetMessage(reason))
    {
        Reason = reason;
    }

    private static string GetMessage(IapUnavailableReason reason) => reason switch
    {
        IapUnavailableReason.Timeout => "외부 스토어 검증 서버 응답 지연으로 서비스를 일시적으로 사용할 수 없습니다.",
        IapUnavailableReason.CircuitOpen => "외부 스토어 검증 서버 장애로 서비스를 일시적으로 사용할 수 없습니다.",
        _ => "IAP 서비스를 일시적으로 사용할 수 없습니다."
    };
}

// IAP 서비스 불가 원인 분류
public enum IapUnavailableReason
{
    // Polly TimeoutRejectedException 발생 — 단일 호출이 타임아웃 초과
    Timeout,

    // Polly BrokenCircuitException 발생 — 서킷브레이커 OPEN 상태
    CircuitOpen
}
