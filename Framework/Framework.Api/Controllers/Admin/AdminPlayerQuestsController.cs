using Framework.Api.Filters;
using Framework.Application.Features.Quest;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 플레이어 퀘스트 진행 상태 조회 컨트롤러
// GET /api/admin/players/{playerId}/quests — 특정 플레이어의 현재 퀘스트 진행 상태 조회
[AdminApiKey]
[ApiController]
[Route("api/admin/players/{playerId:int}/quests")]
public class AdminPlayerQuestsController : ControllerBase
{
    private readonly IQuestAdminService _questAdminService;

    public AdminPlayerQuestsController(IQuestAdminService questAdminService)
    {
        _questAdminService = questAdminService;
    }

    // 플레이어 퀘스트 진행 상태 전체 조회 — 모든 주기(일일/주간/영구) 현재 PeriodKey 기준
    [HttpGet]
    public async Task<IActionResult> GetPlayerQuests(int playerId)
    {
        var progresses = await _questAdminService.GetPlayerProgressAsync(playerId);
        return Ok(progresses);
    }
}
