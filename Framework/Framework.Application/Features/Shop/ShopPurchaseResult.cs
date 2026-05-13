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

    // 보상 테이블이 비어있어 구매 불가 (서버 데이터 설정 오류)
    RewardTableEmpty,

    // 보상 지급 실패 (RewardDispatcher 내부 오류)
    RewardGrantFailed,

    // 중복 요청 — 동일 clientRequestId로 이미 처리됨
    Duplicate,

    // 한도를 초과하는 수량 요청 — 잔여 한도(RemainingQuantity)가 요청 수량보다 적음
    // 잔여 수량 > 0이면 부분 구매 가능하지만 분할 구매는 클라이언트 책임
    LimitWouldExceed,

    // 1회 최대 구매 수량(MaxPerCall) 초과 — 요청 수량이 상품 설정 한도보다 많음
    MaxPerCallExceeded,
}

// 인게임 상점 구매 결과 DTO
// RemainingQuantity: LimitWouldExceed 시 잔여 구매 가능 수량 (null이면 해당 없음)
public record ShopPurchaseResult(ShopPurchaseStatus Status, int? RemainingQuantity = null);
