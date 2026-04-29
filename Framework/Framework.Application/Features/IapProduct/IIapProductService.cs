using Framework.Domain.Enums;

namespace Framework.Application.Features.IapProduct;

// 인앱결제 상품 Admin 관리 서비스 인터페이스
public interface IIapProductService
{
    // 인앱결제 상품 목록 조회 (스토어/유형/활성 여부 필터 + 페이지네이션)
    // TotalCount를 함께 반환하여 Admin 페이지의 페이지네이션 계산에 사용
    Task<(List<IapProductDto> Items, int TotalCount)> GetListAsync(
        IapStore? store, IapProductType? productType, bool? isEnabled, int page, int pageSize);

    // ID로 인앱결제 상품 단건 조회
    Task<IapProductDto?> GetByIdAsync(int id);

    // 인앱결제 상품 생성 — UNIQUE(Store, ProductId) 위반 시 예외 발생
    Task<IapProductDto> CreateAsync(CreateIapProductRequest request);

    // 인앱결제 상품 수정 (Store/ProductId 불변)
    Task<IapProductDto> UpdateAsync(int id, UpdateIapProductRequest request);

    // 인앱결제 상품 소프트 삭제 (IsDeleted = true)
    Task DeleteAsync(int id);
}

// 인앱결제 구매 이력 Admin 조회 서비스 인터페이스 (읽기 전용)
public interface IIapPurchaseAdminService
{
    // 구매 이력 검색 — 플레이어/스토어/상품/상태/기간 필터 + 페이지네이션
    Task<(List<IapPurchaseDto> Items, int TotalCount)> SearchAsync(
        int? playerId, IapStore? store, string? productId,
        IapPurchaseStatus? status, DateTime? from, DateTime? to,
        int page, int pageSize);

    // ID로 구매 이력 단건 조회
    Task<IapPurchaseDto?> GetByIdAsync(int id);
}
