namespace Framework.Application.Features.Shop;

// 상점 상품 목록/단건 조회용 DTO
public record ShopProductDto(
    int Id,
    string Name,
    string Description,
    int PriceItemId,
    string PriceItemName,  // 가격 재화 이름 (표시용)
    int PriceAmount,
    int RewardTableId,
    int DailyLimit,
    int TotalLimit,
    bool IsEnabled,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? MaxPerCall = null  // 1회 구매 최대 수량 — null이면 무제한
);

// 상점 상품 생성 요청 DTO
public record CreateShopProductRequest(
    // 상품 이름
    string Name,

    // 상품 설명
    string Description,

    // 가격 재화 ItemId — 차감할 통화 아이템 ID
    int PriceItemId,

    // 가격 수량
    int PriceAmount,

    // 구매 시 지급할 보상 테이블 ID
    int RewardTableId,

    // 일일 구매 한도 (0=무제한)
    int DailyLimit,

    // 총 구매 한도 (0=무제한)
    int TotalLimit,

    // 활성화 여부
    bool IsEnabled,

    // 정렬 순서
    int SortOrder,

    // 1회 최대 구매 수량 — null이면 무제한
    int? MaxPerCall = null
);

// 상점 상품 수정 요청 DTO — 부분 수정 지원
public record UpdateShopProductRequest(
    // 상품 이름 변경 (null이면 유지)
    string? Name,

    // 상품 설명 변경 (null이면 유지)
    string? Description,

    // 가격 재화 ItemId 변경 (null이면 유지)
    int? PriceItemId,

    // 가격 수량 변경 (null이면 유지)
    int? PriceAmount,

    // 보상 테이블 ID 변경 (null이면 유지)
    int? RewardTableId,

    // 일일 구매 한도 변경 (null이면 유지)
    int? DailyLimit,

    // 총 구매 한도 변경 (null이면 유지)
    int? TotalLimit,

    // 활성화 여부 변경 (null이면 유지)
    bool? IsEnabled,

    // 정렬 순서 변경 (null이면 유지)
    int? SortOrder,

    // 1회 최대 구매 수량 변경 — null이면 유지, 0이면 무제한으로 초기화
    // (UpdateShopProductRequest에서 int?이므로 명시적으로 0을 전달해 무제한 처리 가능)
    int? MaxPerCall = null
);
