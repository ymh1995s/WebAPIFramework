using Framework.Api.Filters;
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
    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] AdminGrantRewardDto dto)
    {
        // 지급할 내용이 있는지 확인
        var hasItems = dto.Items is { Count: > 0 };
        if (dto.Gold <= 0 && dto.Gems <= 0 && dto.Exp <= 0 && !hasItems)
            return BadRequest(new { message = "지급할 보상이 없습니다. (Gold/Gems/Exp/Items 중 하나 이상 입력)" });

        // RewardItem 목록 변환
        var rewardItems = hasItems
            ? dto.Items!.Select(i => new RewardItem(i.ItemId, i.Quantity)).ToArray()
            : null;

        var bundle = new RewardBundle(
            Gold: dto.Gold ?? 0,
            Gems: dto.Gems ?? 0,
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
            Mode: dto.Mode
        );

        var result = await _dispatcher.GrantAsync(request);

        // 플레이어 미존재 시 404 반환
        if (result.IsNotFound)
            return NotFound(new { message = result.Message });

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        if (result.AlreadyGranted)
            return Ok(new { message = "이미 지급된 보상입니다.", alreadyGranted = true });

        return Ok(new
        {
            message = result.Message,
            usedMode = result.UsedMode.ToString(),
            mailId = result.MailId
        });
    }
}

// 수동 보상 지급 요청 DTO
public record AdminGrantRewardDto(
    // 대상 플레이어 ID
    int PlayerId,

    // 멱등성 키 — AdminGrant 내에서 유일해야 함 (예: "2026-04-29-event", "support-ticket-123")
    string SourceKey,

    // 지급 재화 (미입력 시 0)
    int? Gold,
    int? Gems,
    int? Exp,

    // 지급 아이템 목록 (미입력 시 없음)
    List<AdminGrantItemDto>? Items,

    // 지급 방식 (Auto=자동판단, Direct=즉시지급, Mail=우편)
    DispatchMode Mode = DispatchMode.Auto,

    // 우편 지급 시 제목 (Mail 모드)
    string? MailTitle = null,

    // 우편 지급 시 본문 (Mail 모드)
    string? MailBody = null,

    // 우편 만료 일수 (Mail 모드, 기본 30일)
    int? MailExpiresInDays = null
);

// 지급 아이템 단위 DTO
public record AdminGrantItemDto(int ItemId, int Quantity);
