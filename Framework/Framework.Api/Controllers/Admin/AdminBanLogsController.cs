using Framework.Api.Constants;
using Framework.Api.Filters;
using Framework.Application.Features.BanLog;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 밴/밴해제 감사 이력 조회 컨트롤러
// GET /api/admin/ban-logs — 필터 + 페이지네이션 기반 이력 검색
[AdminApiKey]
[ApiController]
[Route("api/admin/ban-logs")]
public class AdminBanLogsController : ControllerBase
{
    private readonly IBanLogService _banLogService;

    public AdminBanLogsController(IBanLogService banLogService)
    {
        _banLogService = banLogService;
    }

    // 밴 이력 검색 — 필터 조건 중 일부만 지정해도 동작 (모두 optional)
    // pageSize는 최소 1, 최대 100으로 클램프
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int? playerId,
        [FromQuery] BanAction? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // 페이지 번호 최솟값 보정 및 pageSize 범위 제한
        page     = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, PaginationLimits.AdminDefault);

        var result = await _banLogService.SearchAsync(playerId, action, from, to, page, pageSize);
        return Ok(result);
    }
}
