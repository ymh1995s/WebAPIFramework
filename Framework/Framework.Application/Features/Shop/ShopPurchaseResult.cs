namespace Framework.Application.Features.Shop;

// 인게임 상점 구매 처리 결과 — ShopPurchaseService가 반환하는 상태값
public enum ShopPurchaseStatus
{
    // 정상 구매 완료
    Success,

    // 상품이 존재하지 않거나 비활성화 상태
    ProductNotFound,

    // 재화 수량 부족 — PriceItemId 기준 잔액 < PriceAmount
    NotEnoughCurrency,

    // 일일 구매 한도 초과
    DailyLimitExceeded,

    // 총 구매 한도 초과
    TotalLimitExceeded,

    // 보상 테이블이 비어있어 구매 불가 (서버 데이터 설정 오류)
    RewardTableEmpty,

    // 보상 지급 실패 (RewardDispatcher 내부 오류)
    RewardGrantFailed,

    // 중복 요청 — 동일 clientRequestId로 이미 처리됨
    Duplicate,
}

// 인게임 상점 구매 결과 DTO
public record ShopPurchaseResult(ShopPurchaseStatus Status);
