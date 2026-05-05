using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 인앱결제 관련 도메인 예외 클래스 모음

// 스토어에 등록된 상품을 찾을 수 없을 때 발생하는 예외
public class IapProductNotFoundException : Exception
{
    public IapStore Store { get; }
    public string ProductId { get; }

    public IapProductNotFoundException(IapStore store, string productId)
        : base($"인앱결제 상품을 찾을 수 없습니다. Store={store}, ProductId={productId}")
    {
        Store = store;
        ProductId = productId;
    }
}

// 영수증 검증 실패 시 발생하는 예외 — 위변조 또는 만료된 영수증
public class IapReceiptInvalidException : Exception
{
    public IapStore Store { get; }

    public IapReceiptInvalidException(IapStore store, string reason)
        : base($"영수증 검증에 실패했습니다. Store={store}, 사유={reason}")
    {
        Store = store;
    }
}

// 구매 토큰 소유자 불일치 예외 — 다른 플레이어의 토큰으로 요청한 경우
public class IapTokenOwnershipMismatchException : Exception
{
    public IapStore Store { get; }
    public string PurchaseToken { get; }

    public IapTokenOwnershipMismatchException(IapStore store, string purchaseToken)
        : base($"구매 토큰의 소유자가 요청 플레이어와 일치하지 않습니다. Store={store}")
    {
        Store = store;
        PurchaseToken = purchaseToken;
    }
}

// 외부 스토어 API 오류 시 발생하는 예외 — 네트워크 장애, API 응답 이상 등
public class IapVerifierException : Exception
{
    public IapStore Store { get; }

    public IapVerifierException(IapStore store, string message)
        : base($"스토어 검증 API 오류가 발생했습니다. Store={store}, 메시지={message}")
    {
        Store = store;
    }
}

// verify 경로 동시성 충돌 재시도 한도(3회) 초과 — 503 응답 매핑 대상
// AdminNotification(Critical)이 발송된 뒤 이 예외를 throw하여 컨트롤러가 503을 반환하도록 함
public class IapVerifyConcurrencyException : Exception
{
    public IapStore Store { get; }
    public string MaskedToken { get; }

    public IapVerifyConcurrencyException(IapStore store, string maskedToken)
        : base($"IAP verify 동시성 충돌 재시도 한도 초과. Store={store}, Token={maskedToken}")
    {
        Store = store;
        MaskedToken = maskedToken;
    }
}
