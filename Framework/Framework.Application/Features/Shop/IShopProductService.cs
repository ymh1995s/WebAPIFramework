namespace Framework.Application.Features.Shop;

// 인게임 상점 상품 Admin 관리 서비스 인터페이스
public interface IShopProductService
{
    // 상점 상품 목록 조회 (활성 여부 필터 + 페이지네이션)
    Task<(List<ShopProductDto> Items, int TotalCount)> GetListAsync(bool? isEnabled, int page, int pageSize);

    // ID로 상점 상품 단건 조회
    Task<ShopProductDto?> GetByIdAsync(int id);

    // 상점 상품 생성
    Task<ShopProductDto> CreateAsync(CreateShopProductRequest request);

    // 상점 상품 수정 (부분 수정 지원)
    Task<ShopProductDto> UpdateAsync(int id, UpdateShopProductRequest request);

    // 상점 상품 소프트 삭제
    Task DeleteAsync(int id);
}
