using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 스토어별 소모성 상품 consume 전략 인터페이스 — Strategy 패턴
// Google Play: purchases.products.consume 호출로 재구매 허용 신고
public interface IIapConsumer
{
    // 이 구현체가 담당하는 스토어 종류
    IapStore Store { get; }

    // 스토어 consume API 호출 — 소모성 상품 재구매 허용 신고
    // 성공 시 정상 반환. 실패 시 IapConsumeException 발생
    Task ConsumeAsync(string productId, string purchaseToken);
}
