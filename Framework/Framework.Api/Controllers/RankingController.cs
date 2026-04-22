using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 랭킹 API 컨트롤러 - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    // 상위 N명 랭킹 조회 (기본 100명)
    [HttpGet("top")]
    public async Task<IActionResult> GetTop([FromQuery] int count = 100)
        => Ok(await _rankingService.GetTopRankingsAsync(count));

    // 내 순위 조회 - JWT에서 PlayerId 추출
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRanking()
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var result = await _rankingService.GetMyRankingAsync(playerId);
        return Ok(result);
    }
}
