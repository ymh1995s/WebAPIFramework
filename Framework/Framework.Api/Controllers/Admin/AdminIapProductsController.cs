using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Features.IapProduct;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 인앱결제 상품 + 구매 이력 CRUD 컨트롤러
// 상품: GET 목록, GET 단건, POST 생성, PUT 수정, DELETE 소프트삭제
// 구매 이력: GET 목록(필터), GET 단건
[AdminApiKey]
[ApiController]
[Route("api/admin/iap")]
public class AdminIapProductsController : ControllerBase
{
    private readonly IIapProductService _productService;
    private readonly IIapPurchaseAdminService _purchaseService;

    public AdminIapProductsController(
        IIapProductService productService,
        IIapPurchaseAdminService purchaseService)
    {
        _productService = productService;
        _purchaseService = purchaseService;
    }

    // ─── 상품 관리 ─────────────────────────────────────────────

    // 인앱결제 상품 목록 조회 (스토어/유형/활성 여부 필터 + 페이지네이션)
    // Admin 페이지가 기대하는 { Items, TotalCount, Page, PageSize } 구조로 래핑해서 반환
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] IapStore? store,
        [FromQuery] IapProductType? productType,
        [FromQuery] bool? isEnabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // pageSize 범위 제한 — 비정상적으로 큰 값 요청 시 DB 부하 방지 (M-37)
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _productService.GetListAsync(store, productType, isEnabled, page, pageSize);
        return Ok(new PagedResultDto<IapProductDto>(items, totalCount, page, pageSize));
    }

    // 인앱결제 상품 단건 조회
    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var result = await _productService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 인앱결제 상품 생성 — UNIQUE(Store, ProductId) 위반 시 409 반환
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateIapProductRequest request)
    {
        try
        {
            var result = await _productService.CreateAsync(request);
            return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            // UNIQUE 위반 — 동일 Store + ProductId 상품 이미 존재
            return Conflict(new MessageResponse(ex.Message));
        }
    }

    // 인앱결제 상품 수정 (Store/ProductId 불변, 나머지 선택적 업데이트)
    [HttpPut("products/{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateIapProductRequest request)
    {
        try
        {
            var result = await _productService.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new MessageResponse($"인앱결제 상품을 찾을 수 없습니다. Id={id}"));
        }
    }

    // 인앱결제 상품 소프트 삭제 (IsDeleted = true)
    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        try
        {
            await _productService.DeleteAsync(id);
            return Ok(new MessageResponse("인앱결제 상품이 삭제되었습니다."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new MessageResponse($"인앱결제 상품을 찾을 수 없습니다. Id={id}"));
        }
    }

    // ─── 구매 이력 조회 ─────────────────────────────────────────

    // 구매 이력 목록 조회 (플레이어/스토어/상품/상태/기간 필터 + 페이지네이션)
    [HttpGet("purchases")]
    public async Task<IActionResult> GetPurchases(
        [FromQuery] int? playerId,
        [FromQuery] IapStore? store,
        [FromQuery] string? productId,
        [FromQuery] IapPurchaseStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // pageSize 범위 제한 — 비정상적으로 큰 값 요청 시 DB 부하 방지 (M-37)
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _purchaseService.SearchAsync(
            playerId, store, productId, status, from, to, page, pageSize);

        return Ok(new IapPurchaseSearchResponse(items, total));
    }

    // 구매 이력 단건 조회
    [HttpGet("purchases/{id:int}")]
    public async Task<IActionResult> GetPurchase(int id)
    {
        var result = await _purchaseService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
