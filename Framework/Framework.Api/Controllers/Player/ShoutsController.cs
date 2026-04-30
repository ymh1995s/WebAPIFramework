using Framework.Api.Extensions;
using Framework.Application.Features.Shout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 1회 공지 API 컨트롤러 — 인증된 플레이어가 접속 시 활성 1회 공지를 조회
[Authorize]
[ApiController]
[Route("api/shouts")]
public class ShoutsController : ControllerBase
{
    private readonly IShoutService _shoutService;

    public ShoutsController(IShoutService shoutService)
    {
        _shoutService = shoutService;
    }

    // GET /api/shouts/active — JWT에서 PlayerId 추출 후 전체+개인 활성 1회 공지 반환
    // 결과가 없어도 빈 배열 반환 (204 아닌 200)
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        // JWT playerId 클레임에서 현재 플레이어 식별
        var playerId = User.GetPlayerIdRequired();
        var shouts = await _shoutService.GetActiveForPlayerAsync(playerId);
        return Ok(shouts);
    }
}
