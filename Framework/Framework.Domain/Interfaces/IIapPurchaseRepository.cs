using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 인앱결제 구매 이력 저장소 인터페이스
public interface IIapPurchaseRepository
{
    // Store + PurchaseToken 조합으로 구매 이력 조회 — 중복 처리 방지에 사용
    Task<IapPurchase?> FindByTokenAsync(IapStore store, string purchaseToken);

    // ID로 단건 조회
    Task<IapPurchase?> GetByIdAsync(int id);

    // Admin 필터 검색 — 플레이어/스토어/상품/상태/기간 필터 + 페이지네이션
    Task<(List<IapPurchase> Items, int TotalCount)> SearchAsync(
        int? playerId,
        IapStore? store,
        string? productId,
        IapPurchaseStatus? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize);

    // 새 구매 이력 추가
    Task AddAsync(IapPurchase purchase);

    // 변경사항 저장
    Task SaveChangesAsync();

    // consume 재시도 대상 조회 — Granted 상태 + Consumable + ConsumedAt 미기록 + 최대 시도 미달
    Task<List<IapPurchase>> FindPendingConsumesAsync(int maxAttempts);
}
