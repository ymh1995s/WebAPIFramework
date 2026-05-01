using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Features.Shout;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 1회 공지 관리 컨트롤러 — X-Admin-Key 헤더 검증 자동 적용
[AdminApiKey]
[ApiController]
[Route("api/admin/shouts")]
public class AdminShoutsController : ControllerBase
{
    private readonly IShoutService _shoutService;

    public AdminShoutsController(IShoutService shoutService)
    {
        _shoutService = shoutService;
    }

    // 1회 공지 목록 조회 — 필터 + 페이지네이션
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? playerId,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _shoutService.GetAllAsync(page, pageSize, playerId, activeOnly);
        return Ok(result);
    }

    // 1회 공지 생성 — PlayerId null이면 전체 대상
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShoutDto dto)
    {
        try
        {
            var result = await _shoutService.CreateAsync(dto);
            return Created(string.Empty, result);
        }
        catch (ArgumentException ex)
        {
            // 입력값 검증 실패
            return BadRequest(new MessageResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            // 존재하지 않는 플레이어
            return NotFound(new MessageResponse(ex.Message));
        }
    }

    // 1회 공지 즉시 비활성화
    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var success = await _shoutService.DeactivateAsync(id);
        return success ? Ok() : NotFound(new MessageResponse("해당 1회 공지를 찾을 수 없습니다."));
    }
}
