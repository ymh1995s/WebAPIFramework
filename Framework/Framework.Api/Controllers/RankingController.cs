using Framework.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 랭킹 API 컨트롤러
[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    // 상위 N명 랭킹 조회 - Admin 전용 (X-Admin-Key 헤더 필요)
    [AdminApiKey]
    [HttpGet("top")]
    public async Task<IActionResult> GetTop([FromQuery] int count = 100)
        => Ok(await _rankingService.GetTopRankingsAsync(count));

    // 내 순위 조회 - 게임 클라이언트 전용 (JWT에서 PlayerId 추출)
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRanking()
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var result = await _rankingService.GetMyRankingAsync(playerId);
        return Ok(result);
    }
}
