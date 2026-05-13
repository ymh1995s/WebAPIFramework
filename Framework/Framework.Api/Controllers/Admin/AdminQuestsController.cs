using Framework.Api.Constants;
using Framework.Api.Filters;
using Framework.Application.Features.Quest;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 퀘스트 관리 컨트롤러
// 퀘스트 정의: GET 목록, GET 단건, POST 생성, PUT 수정, DELETE 소프트 삭제
// 플레이어 퀘스트: GET /api/admin/players/{playerId}/quests
[AdminApiKey]
[ApiController]
[Route("api/admin/quests")]
public class AdminQuestsController : ControllerBase
{
    private readonly IQuestAdminService _questAdminService;

    public AdminQuestsController(IQuestAdminService questAdminService)
    {
        _questAdminService = questAdminService;
    }

    // 퀘스트 정의 목록 조회 — 키워드 + 주기 필터 + 페이지네이션
    [HttpGet]
    public async Task<IActionResult> GetQuests(
        [FromQuery] string? keyword,
        [FromQuery] QuestPeriod? period,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // pageSize 범위 제한 — 비정상 큰 값 요청 시 DB 부하 방지
        pageSize = Math.Clamp(pageSize, 1, PaginationLimits.AdminDefault);
        var result = await _questAdminService.SearchAsync(keyword, period, isActive, page, pageSize);
        return Ok(result);
    }

    // 퀘스트 정의 단건 조회
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetQuest(int id)
    {
        var result = await _questAdminService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // 퀘스트 정의 생성
    [HttpPost]
    public async Task<IActionResult> CreateQuest([FromBody] CreateQuestDefinitionDto dto)
    {
        var result = await _questAdminService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetQuest), new { id = result.Id }, result);
    }

    // 퀘스트 정의 수정 (Period/Code 변경 불가)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateQuest(int id, [FromBody] UpdateQuestDefinitionDto dto)
    {
        var success = await _questAdminService.UpdateAsync(id, dto);
        if (!success) return NotFound();
        return NoContent();
    }

    // 퀘스트 정의 소프트 삭제
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteQuest(int id)
    {
        var success = await _questAdminService.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
