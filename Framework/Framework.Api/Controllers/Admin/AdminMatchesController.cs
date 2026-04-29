using Framework.Api.Filters;
using Framework.Application.Features.AdminMatch;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 매치 이력 조회 컨트롤러
[AdminApiKey]
[ApiController]
[Route("api/admin/matches")]
public class AdminMatchesController : ControllerBase
{
    private readonly IAdminMatchService _service;

    public AdminMatchesController(IAdminMatchService service)
    {
        _service = service;
    }

    // 매치 목록 조회 (다중 필터 + 페이지네이션)
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] MatchFilterDto filter)
    {
        var result = await _service.SearchAsync(filter);
        return Ok(result);
    }

    // 매치 상세 조회 (참가자 포함)
    [HttpGet("{matchId:guid}")]
    public async Task<IActionResult> GetById(Guid matchId)
    {
        var result = await _service.GetByIdAsync(matchId);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
