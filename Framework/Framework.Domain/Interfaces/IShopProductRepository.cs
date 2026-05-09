using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 인게임 상점 상품 저장소 인터페이스
public interface IShopProductRepository
{
    // ID로 단건 조회 (소프트 딜리트 포함 — 관리 목적)
    Task<ShopProduct?> GetByIdAsync(int id);

    // Admin 목록 조회 — 활성 여부 필터 + 페이지네이션 (소프트 딜리트 제외)
    Task<(List<ShopProduct> Items, int TotalCount)> SearchAsync(bool? isEnabled, int page, int pageSize);

    // 클라이언트용 활성 상품 목록 조회 (IsEnabled=true, IsDeleted=false, 정렬 순서 오름차순)
    Task<List<ShopProduct>> GetActiveListAsync();

    // 새 상품 추가
    Task AddAsync(ShopProduct product);

    // 변경사항 저장
    Task SaveChangesAsync();
}
