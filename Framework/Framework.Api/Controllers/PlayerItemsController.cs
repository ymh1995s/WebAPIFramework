using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 플레이어 인벤토리 API 컨트롤러 - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/players/{playerId}/items")]
public class PlayerItemsController : ControllerBase
{
    private readonly IPlayerItemService _playerItemService;

    public PlayerItemsController(IPlayerItemService playerItemService)
    {
        _playerItemService = playerItemService;
    }

    // 플레이어 보유 아이템 조회
    [HttpGet]
    public async Task<IActionResult> GetItems(int playerId)
        => Ok(await _playerItemService.GetByPlayerIdAsync(playerId));
}
