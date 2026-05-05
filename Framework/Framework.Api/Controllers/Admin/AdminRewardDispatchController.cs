using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Features.AdminRewardDispatch;
using Framework.Application.Features.Reward;
using Framework.Domain.Enums;
using Framework.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 수동 보상 지급 컨트롤러
// SourceType은 AdminGrant 고정 — IRewardDispatcher를 경유하여 멱등성/감사 로그 자동 처리
[AdminApiKey]
[ApiController]
[Route("api/admin/reward-dispatch")]
public class AdminRewardDispatchController : ControllerBase
{
    private readonly IRewardDispatcher _dispatcher;

    public AdminRewardDispatchController(IRewardDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    // 수동 단일 보상 지급 — SourceType=AdminGrant 고정
    // mode: Auto(기본), Direct(즉시지급), Mail(우편지급)
    // Gold/Gems는 Items 목록으로 변환하여 지급 (ItemId=1: Gold, ItemId=2: Gems)
    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] AdminGrantRewardDto dto)
    {
        // 지급할 내용이 있는지 확인
        var hasItems = dto.Items is { Count: > 0 };
        if (dto.Exp <= 0 && !hasItems)
            return BadRequest(new MessageResponse("지급할 보상이 없습니다. (Exp/Items 중 하나 이상 입력)"));

        // RewardItem 목록 변환 — Items만 사용 (Gold/Gems는 Items에 ItemId=1/2로 포함)
        var rewardItems = hasItems
            ? dto.Items!.Select(i => new RewardItem(i.ItemId, i.Quantity)).ToArray()
            : null;

        // Gold/Gems는 dto.Items에 ItemId=1(Gold)/2(Gems)로 포함하여 전달 — RewardBundle에 Gold/Gems 필드 없음
        var bundle = new RewardBundle(
            Exp: dto.Exp ?? 0,
            Items: rewardItems
        );

        var request = new GrantRewardRequest(
            PlayerId: dto.PlayerId,
            SourceType: RewardSourceType.AdminGrant,
            SourceKey: dto.SourceKey,
            Bundle: bundle,
            MailTitle: dto.MailTitle ?? "운영팀 보상이 도착했습니다",
            MailBody: dto.MailBody ?? "운영팀에서 보상을 지급했습니다.",
            MailExpiresInDays: dto.MailExpiresInDays ?? 30,
            Mode: dto.Mode,
            // Admin이 수동으로 지급하는 보상 — 행위자는 Admin
            ActorType: AuditActorType.Admin
        );

        var result = await _dispatcher.GrantAsync(request);

        // 플레이어 미존재 시 404 반환
        if (result.IsNotFound)
            return NotFound(new MessageResponse(result.Message));

        if (!result.Success)
            return BadRequest(new MessageResponse(result.Message));

        if (result.AlreadyGranted)
            return Ok(new AdminGrantResponse("이미 지급된 보상입니다.", null, null, true));

        return Ok(new AdminGrantResponse(result.Message, result.UsedMode.ToString(), result.MailId));
    }
}
