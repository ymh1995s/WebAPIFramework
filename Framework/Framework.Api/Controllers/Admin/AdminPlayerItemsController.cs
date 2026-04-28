using Framework.Api.Filters;
using Framework.Application.Features.Item;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// 플레이어 인벤토리 API 컨트롤러 - Admin 전용 (X-Admin-Key 헤더 필요)
[AdminApiKey]
[ApiController]
[Route("api/admin/players/{playerId}/items")]
public class AdminPlayerItemsController : ControllerBase
{
    private readonly IPlayerItemService _playerItemService;

    public AdminPlayerItemsController(IPlayerItemService playerItemService)
    {
        _playerItemService = playerItemService;
    }

    // 특정 플레이어의 보유 아이템 조회
    [HttpGet]
    public async Task<IActionResult> GetItems(int playerId)
        => Ok(await _playerItemService.GetByPlayerIdAsync(playerId));
}
