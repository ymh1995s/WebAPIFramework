using Framework.Api.Filters;
using Framework.Application.Features.AdPolicy;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 광고 정책 CRUD 컨트롤러
// [GET /] 목록, [GET /{id}] 단건, [POST /] 생성, [PUT /{id}] 수정, [DELETE /{id}] 소프트삭제
[AdminApiKey]
[ApiController]
[Route("api/admin/ad-policies")]
public class AdminAdPoliciesController : ControllerBase
{
    private readonly IAdPolicyService _service;

    public AdminAdPoliciesController(IAdPolicyService service)
    {
        _service = service;
    }

    // 광고 정책 목록 조회 (network 필터 + 페이지네이션)
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AdPolicyFilterDto filter)
    {
        var result = await _service.SearchAsync(filter);
        return Ok(result);
    }

    // 광고 정책 단건 조회
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 광고 정책 생성 — UNIQUE(Network, PlacementId) 위반 시 409 Conflict
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdPolicyDto dto)
    {
        var result = await _service.CreateAsync(dto);
        if (result is null)
            return Conflict(new { message = "동일한 Network + PlacementId 조합의 광고 정책이 이미 존재합니다." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // 광고 정책 수정 (RewardTableId, DailyLimit, IsEnabled, Description 변경)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAdPolicyDto dto)
    {
        var success = await _service.UpdateAsync(id, dto);
        if (!success) return NotFound();
        return Ok(new { message = "광고 정책이 수정되었습니다." });
    }

    // 광고 정책 소프트 삭제 (IsDeleted = true)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> SoftDelete(int id)
    {
        var success = await _service.SoftDeleteAsync(id);
        if (!success) return NotFound();
        return Ok(new { message = "광고 정책이 삭제되었습니다." });
    }
}
