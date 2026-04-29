using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 인앱결제 상품 저장소 구현체
public class IapProductRepository : IIapProductRepository
{
    private readonly AppDbContext _db;

    public IapProductRepository(AppDbContext db)
    {
        _db = db;
    }

    // Store + ProductId 조합으로 활성 상품 조회 — 비활성/삭제된 상품 제외
    public async Task<IapProduct?> FindActiveAsync(IapStore store, string productId)
        => await _db.IapProducts
            .FirstOrDefaultAsync(p =>
                p.Store == store &&
                p.ProductId == productId &&
                p.IsEnabled &&
                !p.IsDeleted);

    // ID로 단건 조회 (소프트 딜리트 포함 — 관리 목적)
    public async Task<IapProduct?> GetByIdAsync(int id)
        => await _db.IapProducts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    // Admin 필터 검색 — 스토어/유형/활성 여부 필터 + 페이지네이션 (생성일 내림차순)
    public async Task<(List<IapProduct> Items, int TotalCount)> SearchAsync(
        IapStore? store, IapProductType? productType, bool? isEnabled, int page, int pageSize)
    {
        // 소프트 딜리트된 항목 제외
        var query = _db.IapProducts
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // 스토어 필터
        if (store.HasValue)
            query = query.Where(p => p.Store == store.Value);

        // 상품 유형 필터
        if (productType.HasValue)
            query = query.Where(p => p.ProductType == productType.Value);

        // 활성 여부 필터
        if (isEnabled.HasValue)
            query = query.Where(p => p.IsEnabled == isEnabled.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // 새 인앱결제 상품 추가
    public async Task AddAsync(IapProduct product)
        => await _db.IapProducts.AddAsync(product);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
