using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 인앱결제 구매 이력 저장소 구현체
public class IapPurchaseRepository : IIapPurchaseRepository
{
    private readonly AppDbContext _db;

    public IapPurchaseRepository(AppDbContext db)
    {
        _db = db;
    }

    // Store + PurchaseToken 조합으로 구매 이력 조회 — 중복 처리 방지에 사용
    // 구매 이력은 영구 보존 (소프트 딜리트 없음)
    public async Task<IapPurchase?> FindByTokenAsync(IapStore store, string purchaseToken)
        => await _db.IapPurchases
            .FirstOrDefaultAsync(p =>
                p.Store == store &&
                p.PurchaseToken == purchaseToken);

    // ID로 단건 조회
    public async Task<IapPurchase?> GetByIdAsync(int id)
        => await _db.IapPurchases.FirstOrDefaultAsync(p => p.Id == id);

    // Admin 필터 검색 — 플레이어/스토어/상품/상태/기간 필터 + 페이지네이션 (생성일 내림차순)
    public async Task<(List<IapPurchase> Items, int TotalCount)> SearchAsync(
        int? playerId,
        IapStore? store,
        string? productId,
        IapPurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
    {
        var query = _db.IapPurchases.AsQueryable();

        // 플레이어 ID 필터
        if (playerId.HasValue)
            query = query.Where(p => p.PlayerId == playerId.Value);

        // 스토어 필터
        if (store.HasValue)
            query = query.Where(p => p.Store == store.Value);

        // 상품 ID 부분 일치 필터
        if (!string.IsNullOrWhiteSpace(productId))
            query = query.Where(p => p.ProductId.Contains(productId));

        // 처리 상태 필터
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        // 기간 필터 (생성일 UTC 기준)
        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // 새 구매 이력 추가
    public async Task AddAsync(IapPurchase purchase)
        => await _db.IapPurchases.AddAsync(purchase);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();

    // consume 재시도 대상 조회 — Granted 상태 + Consumable + ConsumedAt null + 시도 횟수 미달
    public async Task<List<IapPurchase>> FindPendingConsumesAsync(int maxAttempts)
        => await _db.IapPurchases
            .Where(p => p.Status == IapPurchaseStatus.Granted
                     && p.ProductType == IapProductType.Consumable
                     && p.ConsumedAt == null
                     && p.ConsumeAttempts < maxAttempts)
            .ToListAsync();
}
