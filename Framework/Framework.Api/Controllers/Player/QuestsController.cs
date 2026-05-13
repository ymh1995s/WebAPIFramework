using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Application.Features.Quest;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 퀘스트 컨트롤러 (플레이어 전용)
// GET  /api/quests?period= — 플레이어 퀘스트 목록 조회
// POST /api/quests/{questId}/claim — 퀘스트 보상 수령
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/quests")]
public class QuestsController : ControllerBase
{
    private readonly IQuestProgressService _questProgressService;

    public QuestsController(IQuestProgressService questProgressService)
    {
        _questProgressService = questProgressService;
    }

    // 플레이어 퀘스트 목록 조회 — 주기(period)별 진행 상태 반환
    // period 파라미터: 0=Daily, 1=Weekly, 2=Permanent (미입력 시 Daily)
    [HttpGet]
    public async Task<IActionResult> GetQuests([FromQuery] QuestPeriod period = QuestPeriod.Daily)
    {
        var playerId = User.GetPlayerIdRequired();
        var progresses = await _questProgressService.GetProgressListAsync(playerId, period);
        return Ok(progresses);
    }

    // 퀘스트 보상 수령 처리 — 완료된 퀘스트에 한해 보상 지급
    // 멱등: 이미 수령한 퀘스트 재요청 시 200 반환 (alreadyClaimed)
    [HttpPost("{questId:int}/claim")]
    public async Task<IActionResult> Claim(int questId)
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _questProgressService.ClaimAsync(playerId, questId);

        return result switch
        {
            // 보상 수령 성공
            "success" => Ok(new { message = "퀘스트 보상이 지급되었습니다." }),

            // 이미 수령한 퀘스트 — 멱등 응답
            "alreadyClaimed" => Ok(new { message = "이미 수령한 퀘스트입니다." }),

            // 달성 미완료 — 400
            "notEligible" => BadRequest(new { message = "퀘스트 조건이 달성되지 않았습니다." }),

            // 퀘스트 없음/비활성 — 404
            "notFound" => NotFound(new { message = "퀘스트를 찾을 수 없습니다." }),

            // 알 수 없는 상태 — 500
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "퀘스트 보상 처리 중 오류가 발생했습니다." })
        };
    }
}
