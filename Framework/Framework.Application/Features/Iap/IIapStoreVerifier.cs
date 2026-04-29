using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 스토어별 영수증 검증 전략 인터페이스 — Strategy 패턴으로 Google/Apple 분기 처리
public interface IIapStoreVerifier
{
    // 이 구현체가 담당하는 스토어 종류
    IapStore Store { get; }

    // 영수증 검증 수행 — 외부 스토어 API를 호출하여 구매 유효성 확인
    Task<IapReceiptVerified> VerifyAsync(string productId, string purchaseToken);
}
