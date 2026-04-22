using Framework.Api.Filters;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 플레이어 인벤토리 API 컨트롤러 - Admin 전용 (X-Admin-Key 헤더 필요)
[AdminApiKey]
[ApiController]
[Route("api/players/{playerId}/items")]
public class PlayerItemsController : ControllerBase
{
    private readonly IPlayerItemService _playerItemService;

    public PlayerItemsController(IPlayerItemService playerItemService)
    {
        _playerItemService = playerItemService;
    }

    // 특정 플레이어의 보유 아이템 조회
    [HttpGet]
    public async Task<IActionResult> GetItems(int playerId)
        => Ok(await _playerItemService.GetByPlayerIdAsync(playerId));
}
