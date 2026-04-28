using Framework.Api.Filters;
using Framework.Application.Features.DailyReward;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// 일일 보상 슬롯 Admin API 컨트롤러 (X-Admin-Key 헤더 필요)
// Current(이번 달) / Next(다음 달) 2슬롯의 Day별 보상 조회·수정
[AdminApiKey]
[ApiController]
[Route("api/admin/daily-rewards/slots")]
public class AdminDailyRewardSlotsController : ControllerBase
{
    private readonly IDailyRewardSlotService _slotService;

    public AdminDailyRewardSlotsController(IDailyRewardSlotService slotService)
    {
        _slotService = slotService;
    }

    // 슬롯 전체 28개 Day 조회
    // slot: "current" 또는 "next" (대소문자 무관)
    [HttpGet("{slot}")]
    public async Task<IActionResult> GetSlot(string slot)
    {
        // URL 파라미터를 내부 상수값으로 정규화 (current → Current, next → Next)
        var normalizedSlot = NormalizeSlot(slot);
        if (normalizedSlot is null)
            return BadRequest("slot은 'current' 또는 'next' 여야 합니다.");

        var result = await _slotService.GetSlotAsync(normalizedSlot);
        return Ok(result);
    }

    // 특정 슬롯의 특정 Day 보상 수정
    // slot: "current" 또는 "next", day: 1~28
    [HttpPut("{slot}/days/{day:int}")]
    public async Task<IActionResult> UpdateSlotDay(string slot, int day, [FromBody] UpdateSlotDayDto dto)
    {
        var normalizedSlot = NormalizeSlot(slot);
        if (normalizedSlot is null)
            return BadRequest("slot은 'current' 또는 'next' 여야 합니다.");

        try
        {
            await _slotService.UpdateSlotDayAsync(normalizedSlot, day, dto);
            return Ok();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // URL 슬롯 파라미터 정규화 (대소문자 무관 → 내부 상수값으로 변환)
    private static string? NormalizeSlot(string slot) => slot.ToLower() switch
    {
        "current" => RewardSlotKind.Current,
        "next" => RewardSlotKind.Next,
        _ => null
    };
}
