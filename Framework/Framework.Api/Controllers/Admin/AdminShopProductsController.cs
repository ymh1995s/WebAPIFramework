using Framework.Api.Constants;
using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Features.Shop;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 인게임 상점 상품 CRUD 컨트롤러
// 상품: GET 목록, GET 단건, POST 생성, PUT 수정, DELETE 소프트삭제
[AdminApiKey]
[ApiController]
[Route("api/admin/shop/products")]
public class AdminShopProductsController : ControllerBase
{
    private readonly IShopProductService _service;

    public AdminShopProductsController(IShopProductService service)
    {
        _service = service;
    }

    // 상점 상품 목록 조회 (활성 여부 필터 + 페이지네이션)
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] bool? isEnabled,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // pageSize 범위 제한 — 비정상적으로 큰 값 요청 시 DB 부하 방지
        pageSize = Math.Clamp(pageSize, 1, PaginationLimits.AdminDefault);

        var (items, totalCount) = await _service.GetListAsync(isEnabled, page, pageSize);
        return Ok(new PagedResultDto<ShopProductDto>(items, totalCount, page, pageSize));
    }

    // 상점 상품 단건 조회
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 상점 상품 생성
    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateShopProductRequest request)
    {
        var result = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
    }

    // 상점 상품 수정 (부분 수정 지원)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateShopProductRequest request)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new MessageResponse($"상점 상품을 찾을 수 없습니다. Id={id}"));
        }
    }

    // 상점 상품 소프트 삭제
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Ok(new MessageResponse("상점 상품이 삭제되었습니다."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new MessageResponse($"상점 상품을 찾을 수 없습니다. Id={id}"));
        }
    }
}
