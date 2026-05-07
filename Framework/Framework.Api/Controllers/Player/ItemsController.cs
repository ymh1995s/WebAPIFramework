using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Application.Features.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 아이템 API 컨트롤러 (유저 전용) - 인증된 사용자만 접근 가능
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IPlayerItemService _playerItemService;

    public ItemsController(IPlayerItemService playerItemService)
    {
        _playerItemService = playerItemService;
    }

    // 인벤토리 조회 - JWT에서 PlayerId 추출하여 본인 보유 아이템 목록 반환 (통화 아이템 포함)
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory()
    {
        var playerId = User.GetPlayerIdRequired();
        var items = await _playerItemService.GetByPlayerIdAsync(playerId);
        return Ok(items);
    }
}
