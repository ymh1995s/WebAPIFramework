using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Api.Requests;
using Framework.Application.Features.Item;
using Framework.Application.Features.ItemUse;
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
    private readonly IItemUseService _itemUseService;

    public ItemsController(IPlayerItemService playerItemService, IItemUseService itemUseService)
    {
        _playerItemService = playerItemService;
        _itemUseService = itemUseService;
    }

    // 인벤토리 조회 - JWT에서 PlayerId 추출하여 본인 보유 아이템 목록 반환 (통화 아이템 포함)
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory()
    {
        var playerId = User.GetPlayerIdRequired();
        var items = await _playerItemService.GetByPlayerIdAsync(playerId);
        return Ok(items);
    }

    // 아이템 사용 — 수량 차감 + 보상 지급(UseRewardTableId 지정 시)
    // ClientRequestId는 클라이언트가 UUID로 생성하여 전달 — 동일 요청의 중복 처리 방지
    [HttpPost("{itemId}/use")]
    public async Task<IActionResult> UseItem(int itemId, [FromBody] UseItemRequest request)
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _itemUseService.UseItemAsync(playerId, itemId, request.ClientRequestId, request.Quantity);

        return result.Status switch
        {
            // 정상 처리 — 수량 차감 및 보상 지급 완료
            ItemUseResultStatus.Success => Ok(new { message = "아이템 사용이 완료되었습니다." }),
            // 수량 부족 또는 미보유 — 클라이언트 오류
            ItemUseResultStatus.NotEnoughQuantity => BadRequest(new { message = "아이템 수량이 부족합니다." }),
            ItemUseResultStatus.ItemNotFound => BadRequest(new { message = "보유하지 않은 아이템입니다." }),
            // 중복 요청 — 이미 처리된 clientRequestId
            ItemUseResultStatus.Duplicate => Conflict(new { message = "이미 처리된 요청입니다." }),
            // 보상 테이블 비어있음 — 서버 데이터 설정 오류로 처리 불가
            ItemUseResultStatus.RewardTableEmpty => UnprocessableEntity(new { message = "보상 테이블이 비어있어 아이템을 사용할 수 없습니다." }),
            // 보상 지급 실패 — RewardDispatcher 내부 오류 또는 플레이어 미존재 (서버 내부 오류)
            ItemUseResultStatus.RewardGrantFailed => StatusCode(500, new { message = "보상 지급 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요." }),
            _ => StatusCode(500, new { message = "알 수 없는 오류가 발생했습니다." })
        };
    }
}
