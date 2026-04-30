using Framework.Api.Filters;
using Framework.Application.Features.Exp;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// 레벨 임계값 Admin 컨트롤러 — 조회 및 일괄 교체 기능 제공
[AdminApiKey]
[ApiController]
[Route("api/admin/level-thresholds")]
public class AdminLevelThresholdsController : ControllerBase
{
    private readonly ILevelTableAdminService _service;

    public AdminLevelThresholdsController(ILevelTableAdminService service)
    {
        _service = service;
    }

    // 전체 레벨 임계값 목록 조회 (레벨 오름차순)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    // 레벨 테이블 전체 교체 — 기존 데이터를 모두 지우고 새 목록으로 대체
    [HttpPut]
    public async Task<IActionResult> ReplaceAll([FromBody] ReplaceAllLevelThresholdsRequest request)
    {
        try
        {
            await _service.ReplaceAllAsync(request.Items);
            return Ok(new { message = "레벨 테이블이 업데이트되었습니다." });
        }
        catch (ArgumentException ex)
        {
            // 검증 실패 — 잘못된 요청 데이터
            return BadRequest(new { message = ex.Message });
        }
    }
}
