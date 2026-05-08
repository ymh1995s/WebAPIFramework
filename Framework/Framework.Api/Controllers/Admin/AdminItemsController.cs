using Framework.Api.Filters;
using Framework.Api.Requests;
using Framework.Application.Common;
using Framework.Application.Features.Item;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 아이템 마스터 관리 컨트롤러
[AdminApiKey]
[ApiController]
[Route("api/admin/items")]
public class AdminItemsController : ControllerBase
{
    private readonly IItemMasterService _itemMasterService;

    public AdminItemsController(IItemMasterService itemMasterService)
    {
        _itemMasterService = itemMasterService;
    }

    // 전체 아이템 목록 조회
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _itemMasterService.GetAllAsync());

    // 아이템 생성
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemDto dto)
    {
        var created = await _itemMasterService.CreateAsync(dto);
        return Created($"api/admin/items/{created.Id}", created);
    }

    // 아이템 수정
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateItemDto dto)
    {
        await _itemMasterService.UpdateAsync(id, dto);
        return NoContent();
    }

    // 보유 플레이어 수 조회
    [HttpGet("{id}/holders")]
    public async Task<IActionResult> GetHolderCount(int id)
    {
        var count = await _itemMasterService.GetHolderCountAsync(id);
        return Ok(new CountResponse(count));
    }

    // 아이템 소프트 삭제
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _itemMasterService.DeleteAsync(id);
        return NoContent();
    }

    // 아이템 사용 효과 RewardTable ID 조회
    [HttpGet("{id}/use-effect")]
    public async Task<IActionResult> GetUseEffect(int id)
    {
        var rewardTableId = await _itemMasterService.GetUseRewardTableIdAsync(id);
        return Ok(new { RewardTableId = rewardTableId });
    }

    // 아이템 사용 효과 RewardTable ID 설정 — null 전달 시 보상 없음으로 초기화
    [HttpPut("{id}/use-effect")]
    public async Task<IActionResult> SetUseEffect(int id, [FromBody] SetUseEffectRequest request)
    {
        await _itemMasterService.SetUseRewardTableIdAsync(id, request.RewardTableId);
        return NoContent();
    }
}
