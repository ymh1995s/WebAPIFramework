// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Application.Content.Stage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Content.Player;

// 스테이지 클라이언트용 컨트롤러
// - GET  /api/stages              : 활성 스테이지 목록
// - GET  /api/stages/progress     : 내 진행 현황
// - POST /api/stages/{stageId}/complete : 스테이지 클리어 완료
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/stages")]
public class StagesController : ControllerBase
{
    private readonly IStageClearService _service;
    private readonly ILogger<StagesController> _logger;

    public StagesController(IStageClearService service, ILogger<StagesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // 활성 스테이지 목록 조회 — 클라이언트 로비 화면용
    [HttpGet]
    public async Task<IActionResult> GetStages()
    {
        var stages = await _service.GetActiveStagesAsync();
        return Ok(stages);
    }

    // 내 스테이지 진행 현황 조회 — 잠금/클리어 여부 포함
    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        // JWT 클레임에서 PlayerId 추출 — 없으면 null 반환
        var playerId = User.GetPlayerId();
        if (playerId is null)
            return Unauthorized();

        var progress = await _service.GetProgressAsync(playerId.Value);
        return Ok(progress);
    }

    // 스테이지 클리어 완료 처리
    // [성공] 200 + StageClearResponseDto
    // [스테이지 없음/비활성] 404
    // [선행 스테이지 미클리어] 409 Conflict
    [HttpPost("{stageId:int}/complete")]
    public async Task<IActionResult> Complete(int stageId, [FromBody] StageClearRequestDto request)
    {
        // JWT 클레임에서 PlayerId 추출 — 없으면 null 반환
        var playerId = User.GetPlayerId();
        if (playerId is null)
            return Unauthorized();

        try
        {
            var result = await _service.CompleteAsync(playerId.Value, stageId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            // 스테이지 없음 또는 비활성
            _logger.LogWarning(ex, "스테이지 클리어 실패 — 스테이지 없음 PlayerId: {PlayerId}, StageId: {StageId}",
                playerId, stageId);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // 선행 스테이지 미클리어
            _logger.LogWarning(ex, "스테이지 클리어 실패 — 선행 조건 미충족 PlayerId: {PlayerId}, StageId: {StageId}",
                playerId, stageId);
            return Conflict(new { message = ex.Message });
        }
    }
}
