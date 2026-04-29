using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainIapProduct = Framework.Domain.Entities.IapProduct;

namespace Framework.Application.Features.IapProduct;

// 인앱결제 상품 Admin 관리 서비스 구현체
public class IapProductService : IIapProductService
{
    private readonly IIapProductRepository _productRepo;
    private readonly ILogger<IapProductService> _logger;

    public IapProductService(
        IIapProductRepository productRepo,
        ILogger<IapProductService> logger)
    {
        _productRepo = productRepo;
        _logger = logger;
    }

    // 인앱결제 상품 목록 조회 (필터 + 페이지네이션)
    // TotalCount를 함께 반환하여 Admin 페이지의 페이지네이션이 정확하게 동작하도록 함
    public async Task<(List<IapProductDto> Items, int TotalCount)> GetListAsync(
        IapStore? store, IapProductType? productType, bool? isEnabled, int page, int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;

        var (items, totalCount) = await _productRepo.SearchAsync(store, productType, isEnabled, safePage, safePageSize);
        return (items.Select(ToDto).ToList(), totalCount);
    }

    // ID로 인앱결제 상품 단건 조회
    public async Task<IapProductDto?> GetByIdAsync(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        return product is null ? null : ToDto(product);
    }

    // 인앱결제 상품 생성 — UNIQUE(Store, ProductId) 위반 시 예외 발생
    public async Task<IapProductDto> CreateAsync(CreateIapProductRequest request)
    {
        var product = new DomainIapProduct
        {
            Store = request.Store,
            ProductId = request.ProductId.Trim(),
            ProductType = request.ProductType,
            RewardTableId = request.RewardTableId,
            Description = request.Description.Trim(),
            IsEnabled = request.IsEnabled,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _logger.LogDebug(
            "인앱결제 상품 생성 시도 — Store: {Store}, ProductId: {ProductId}",
            request.Store, request.ProductId);

        await _productRepo.AddAsync(product);

        try
        {
            await _productRepo.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // UNIQUE 위반 — 동일 Store + ProductId 상품이 이미 존재함
            _logger.LogWarning(
                "인앱결제 상품 생성 실패 — UNIQUE 위반 (Store: {Store}, ProductId: {ProductId})",
                request.Store, request.ProductId);
            throw new InvalidOperationException(
                $"동일한 Store+ProductId 상품이 이미 존재합니다. Store={request.Store}, ProductId={request.ProductId}");
        }

        _logger.LogInformation(
            "인앱결제 상품 생성 완료 — Id: {Id}, Store: {Store}, ProductId: {ProductId}",
            product.Id, product.Store, product.ProductId);

        return ToDto(product);
    }

    // 인앱결제 상품 수정 — Store/ProductId 불변, 나머지 필드 선택적 업데이트
    public async Task<IapProductDto> UpdateAsync(int id, UpdateIapProductRequest request)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null || product.IsDeleted)
            throw new KeyNotFoundException($"인앱결제 상품을 찾을 수 없습니다. Id={id}");

        // null이 아닌 필드만 업데이트 (부분 수정 지원)
        if (request.ProductType.HasValue)
            product.ProductType = request.ProductType.Value;

        if (request.RewardTableId.HasValue)
            product.RewardTableId = request.RewardTableId == 0 ? null : request.RewardTableId;

        if (request.Description is not null)
            product.Description = request.Description.Trim();

        if (request.IsEnabled.HasValue)
            product.IsEnabled = request.IsEnabled.Value;

        // 수정 시각 갱신
        product.UpdatedAt = DateTime.UtcNow;

        await _productRepo.SaveChangesAsync();

        _logger.LogInformation(
            "인앱결제 상품 수정 완료 — Id: {Id}, Store: {Store}, ProductId: {ProductId}",
            id, product.Store, product.ProductId);

        return ToDto(product);
    }

    // 인앱결제 상품 소프트 삭제 — IsDeleted = true로 논리 삭제
    public async Task DeleteAsync(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null)
            throw new KeyNotFoundException($"인앱결제 상품을 찾을 수 없습니다. Id={id}");

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;

        await _productRepo.SaveChangesAsync();

        _logger.LogInformation(
            "인앱결제 상품 소프트 삭제 — Id: {Id}, Store: {Store}, ProductId: {ProductId}",
            id, product.Store, product.ProductId);
    }

    // 엔티티 → DTO 변환 헬퍼
    private static IapProductDto ToDto(DomainIapProduct p) => new(
        p.Id,
        p.Store,
        p.ProductId,
        p.ProductType,
        p.RewardTableId,
        p.Description,
        p.IsEnabled,
        p.CreatedAt,
        p.UpdatedAt
    );

    // PostgreSQL UNIQUE 제약 위반 여부 확인
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
           || (ex.InnerException?.GetType().Name == "PostgresException" &&
               (ex.InnerException?.Message.Contains("unique") == true ||
                ex.InnerException?.Message.Contains("duplicate") == true));
}
