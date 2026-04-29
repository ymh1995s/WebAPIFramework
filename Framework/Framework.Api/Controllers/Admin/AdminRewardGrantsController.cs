using Framework.Api.Filters;
using Framework.Application.Features.RewardGrant;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 보상 지급 이력 조회 컨트롤러 (읽기 전용)
[AdminApiKey]
[ApiController]
[Route("api/admin/reward-grants")]
public class AdminRewardGrantsController : ControllerBase
{
    private readonly IRewardGrantQueryService _service;

    public AdminRewardGrantsController(IRewardGrantQueryService service)
    {
        _service = service;
    }

    // 보상 지급 이력 목록 조회 (다중 필터 + 페이지네이션)
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] RewardGrantFilterDto filter)
    {
        var result = await _service.SearchAsync(filter);
        return Ok(result);
    }

    // 보상 지급 이력 단건 상세 조회 (BundleSnapshot 포함)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
