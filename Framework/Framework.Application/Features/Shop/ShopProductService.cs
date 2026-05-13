using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Shop;

// 인게임 상점 상품 Admin 관리 서비스 구현체
public class ShopProductService : IShopProductService
{
    private readonly IShopProductRepository _repo;
    private readonly ILogger<ShopProductService> _logger;

    public ShopProductService(
        IShopProductRepository repo,
        ILogger<ShopProductService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // 상점 상품 목록 조회 (활성 여부 필터 + 페이지네이션)
    public async Task<(List<ShopProductDto> Items, int TotalCount)> GetListAsync(bool? isEnabled, int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;

        var (items, totalCount) = await _repo.SearchAsync(isEnabled, safePage, safePageSize);
        return (items.Select(ToDto).ToList(), totalCount);
    }

    // ID로 상점 상품 단건 조회
    public async Task<ShopProductDto?> GetByIdAsync(int id)
    {
        var product = await _repo.GetByIdAsync(id);
        return product is null ? null : ToDto(product);
    }

    // 상점 상품 생성
    public async Task<ShopProductDto> CreateAsync(CreateShopProductRequest request)
    {
        var product = new ShopProduct
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            PriceItemId = request.PriceItemId,
            PriceAmount = request.PriceAmount,
            RewardTableId = request.RewardTableId,
            DailyLimit = request.DailyLimit,
            TotalLimit = request.TotalLimit,
            IsEnabled = request.IsEnabled,
            SortOrder = request.SortOrder,
            // MaxPerCall — null이면 무제한, 값이 있으면 1회 최대 수량으로 설정
            MaxPerCall = request.MaxPerCall,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(product);
        await _repo.SaveChangesAsync();

        _logger.LogInformation(
            "상점 상품 생성 완료 — Id: {Id}, Name: {Name}",
            product.Id, product.Name);

        return ToDto(product);
    }

    // 상점 상품 수정 — null이 아닌 필드만 선택적 업데이트
    public async Task<ShopProductDto> UpdateAsync(int id, UpdateShopProductRequest request)
    {
        var product = await _repo.GetByIdAsync(id);
        if (product is null || product.IsDeleted)
            throw new KeyNotFoundException($"상점 상품을 찾을 수 없습니다. Id={id}");

        // null이 아닌 필드만 업데이트 (부분 수정 지원)
        if (request.Name is not null)
            product.Name = request.Name.Trim();

        if (request.Description is not null)
            product.Description = request.Description.Trim();

        if (request.PriceItemId.HasValue)
            product.PriceItemId = request.PriceItemId.Value;

        if (request.PriceAmount.HasValue)
            product.PriceAmount = request.PriceAmount.Value;

        if (request.RewardTableId.HasValue)
            product.RewardTableId = request.RewardTableId.Value;

        if (request.DailyLimit.HasValue)
            product.DailyLimit = request.DailyLimit.Value;

        if (request.TotalLimit.HasValue)
            product.TotalLimit = request.TotalLimit.Value;

        if (request.IsEnabled.HasValue)
            product.IsEnabled = request.IsEnabled.Value;

        if (request.SortOrder.HasValue)
            product.SortOrder = request.SortOrder.Value;

        // MaxPerCall 업데이트 — 명시적으로 null이 아닌 경우만 변경
        // UpdateShopProductRequest.MaxPerCall이 int?이므로 null이면 유지, 0이면 DB null(무제한)으로 설정
        if (request.MaxPerCall.HasValue)
            product.MaxPerCall = request.MaxPerCall.Value == 0 ? null : request.MaxPerCall.Value;

        // 수정 시각 갱신
        product.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        _logger.LogInformation(
            "상점 상품 수정 완료 — Id: {Id}, Name: {Name}",
            id, product.Name);

        return ToDto(product);
    }

    // 상점 상품 소프트 삭제 — IsDeleted = true로 논리 삭제
    public async Task DeleteAsync(int id)
    {
        var product = await _repo.GetByIdAsync(id);
        if (product is null)
            throw new KeyNotFoundException($"상점 상품을 찾을 수 없습니다. Id={id}");

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync();

        _logger.LogInformation(
            "상점 상품 소프트 삭제 — Id: {Id}, Name: {Name}",
            id, product.Name);
    }

    // 엔티티 → DTO 변환 헬퍼 (PriceItem 네비게이션 속성 포함)
    private static ShopProductDto ToDto(ShopProduct p) => new(
        p.Id,
        p.Name,
        p.Description,
        p.PriceItemId,
        p.PriceItem?.Name ?? $"ItemId:{p.PriceItemId}",  // 네비게이션 미로드 시 fallback
        p.PriceAmount,
        p.RewardTableId,
        p.DailyLimit,
        p.TotalLimit,
        p.IsEnabled,
        p.SortOrder,
        p.CreatedAt,
        p.UpdatedAt,
        p.MaxPerCall
    );
}
