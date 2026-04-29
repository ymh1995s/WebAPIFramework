using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using DomainIapPurchase = Framework.Domain.Entities.IapPurchase;

namespace Framework.Application.Features.IapProduct;

// 인앱결제 구매 이력 Admin 조회 서비스 구현체 (읽기 전용)
public class IapPurchaseAdminService : IIapPurchaseAdminService
{
    private readonly IIapPurchaseRepository _purchaseRepo;
    private readonly ILogger<IapPurchaseAdminService> _logger;

    public IapPurchaseAdminService(
        IIapPurchaseRepository purchaseRepo,
        ILogger<IapPurchaseAdminService> logger)
    {
        _purchaseRepo = purchaseRepo;
        _logger = logger;
    }

    // 구매 이력 검색 — Repository SearchAsync 위임
    public async Task<(List<IapPurchaseDto> Items, int TotalCount)> SearchAsync(
        int? playerId, IapStore? store, string? productId,
        IapPurchaseStatus? status, DateTime? from, DateTime? to,
        int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;

        var (items, total) = await _purchaseRepo.SearchAsync(
            playerId, store, productId, status, from, to, safePage, safePageSize);

        var dtos = items.Select(ToDto).ToList();
        return (dtos, total);
    }

    // ID로 구매 이력 단건 조회
    public async Task<IapPurchaseDto?> GetByIdAsync(int id)
    {
        var purchase = await _purchaseRepo.GetByIdAsync(id);
        return purchase is null ? null : ToDto(purchase);
    }

    // 엔티티 → DTO 변환 헬퍼
    private static IapPurchaseDto ToDto(DomainIapPurchase p) => new(
        p.Id,
        p.PlayerId,
        p.Store,
        p.ProductId,
        p.PurchaseToken,
        p.OrderId,
        p.Status,
        p.PurchaseTimeUtc,
        p.GrantedAt,
        p.RefundedAt,
        p.FailureReason,
        p.CreatedAt
    );
}
