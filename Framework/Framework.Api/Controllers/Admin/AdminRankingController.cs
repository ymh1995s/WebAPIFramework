using Framework.Api.Filters;
using Framework.Application.Features.Ranking;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// 랭킹 Admin API 컨트롤러 - Admin 전용 (X-Admin-Key 헤더 필요)
[AdminApiKey]
[ApiController]
[Route("api/admin/ranking")]
public class AdminRankingController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public AdminRankingController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    // 상위 N명 랭킹 조회 - Admin 전용
    [HttpGet("top")]
    public async Task<IActionResult> GetTop([FromQuery] int count = 100)
        => Ok(await _rankingService.GetTopRankingsAsync(count));
}
