using Framework.Api.Extensions;
using Framework.Application.Features.Ranking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 랭킹 API 컨트롤러 - 게임 클라이언트 전용 (공개/Authorize 액션만 보유)
[ApiController]
[Route("api/[controller]")]
public class RankingController : ControllerBase
{
    private readonly IRankingService _rankingService;

    public RankingController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    // 내 순위 조회 - 게임 클라이언트 전용 (JWT에서 PlayerId 추출)
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRanking()
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _rankingService.GetMyRankingAsync(playerId);
        return Ok(result);
    }
}
