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
    DateTime UpdatedAt
);

// 상점 상품 생성 요청 DTO
public record CreateShopProductRequest(
    // 상품 이름
    string Name,

    // 상품 설명
    string Description,

    // 가격 재화 ItemId (예: Gold=1, Gems=2)
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
    int SortOrder
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
    int? SortOrder
);
