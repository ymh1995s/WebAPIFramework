using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 인게임 상점 상품 저장소 구현체
public class ShopProductRepository : IShopProductRepository
{
    private readonly AppDbContext _db;

    public ShopProductRepository(AppDbContext db)
    {
        _db = db;
    }

    // ID로 단건 조회 — 소프트 딜리트 제외, PriceItem 네비게이션 포함
    public async Task<ShopProduct?> GetByIdAsync(int id)
        => await _db.ShopProducts
            .Include(p => p.PriceItem)
            .Include(p => p.RewardTable)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    // Admin 목록 조회 — 활성 여부 필터 + 페이지네이션 (정렬 순서 → 생성일 내림차순)
    public async Task<(List<ShopProduct> Items, int TotalCount)> SearchAsync(bool? isEnabled, int page, int pageSize)
    {
        // 소프트 딜리트된 항목 제외
        var query = _db.ShopProducts
            .Include(p => p.PriceItem)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // 활성 여부 필터 (null이면 전체 조회)
        if (isEnabled.HasValue)
            query = query.Where(p => p.IsEnabled == isEnabled.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.SortOrder)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // 클라이언트용 활성 상품 목록 — IsEnabled=true, IsDeleted=false, 정렬 순서 오름차순
    public async Task<List<ShopProduct>> GetActiveListAsync()
        => await _db.ShopProducts
            .Include(p => p.PriceItem)
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

    // 새 상점 상품 추가
    public async Task AddAsync(ShopProduct product)
        => await _db.ShopProducts.AddAsync(product);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
