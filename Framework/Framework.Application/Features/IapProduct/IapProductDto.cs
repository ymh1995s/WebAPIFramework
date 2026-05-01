using Framework.Domain.Enums;

namespace Framework.Application.Features.IapProduct;

// 인앱결제 상품 목록/단건 조회용 DTO
public record IapProductDto(
    int Id,
    IapStore Store,
    string ProductId,
    IapProductType ProductType,
    int? RewardTableId,
    string Description,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// 인앱결제 상품 생성 요청 DTO
public record CreateIapProductRequest(
    // 스토어 종류 (Google Play / Apple App Store)
    IapStore Store,

    // 스토어 상품 식별자 (SKU)
    string ProductId,

    // 상품 유형 (소모성 / 비소모성)
    IapProductType ProductType,

    // 연결할 보상 테이블 ID (null이면 보상 없음)
    int? RewardTableId,

    // 상품 설명 (Admin 표시용)
    string Description,

    // 상품 활성화 여부
    bool IsEnabled
);

// 인앱결제 상품 수정 요청 DTO — Store/ProductId는 불변
public record UpdateIapProductRequest(
    // 상품 유형 변경 (null이면 유지)
    IapProductType? ProductType,

    // 연결할 보상 테이블 ID 변경 (null이면 유지, 명시적으로 제거하려면 0 사용)
    int? RewardTableId,

    // 상품 설명 변경 (null이면 유지)
    string? Description,

    // 활성화 여부 변경 (null이면 유지)
    bool? IsEnabled
);

// 인앱결제 구매 이력 검색 결과 응답
public record IapPurchaseSearchResponse(List<IapPurchaseDto> Items, int Total);

// 인앱결제 구매 이력 조회용 DTO
public record IapPurchaseDto(
    int Id,
    int PlayerId,
    IapStore Store,
    string ProductId,
    string PurchaseToken,
    string? OrderId,
    IapPurchaseStatus Status,
    DateTime? PurchaseTimeUtc,
    DateTime? GrantedAt,
    DateTime? RefundedAt,
    string? FailureReason,
    DateTime CreatedAt
);
