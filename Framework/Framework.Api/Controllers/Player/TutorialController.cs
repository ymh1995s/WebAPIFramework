using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Api.Requests;
using Framework.Application.Features.Tutorial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 튜토리얼 진행 상태 API 컨트롤러 (유저 전용)
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/[controller]")]
public class TutorialController : ControllerBase
{
    private readonly ITutorialService _tutorialService;

    public TutorialController(ITutorialService tutorialService)
    {
        _tutorialService = tutorialService;
    }

    // 튜토리얼 진행 상태 전체 조회 — 앱 시작 시 클라이언트가 한 번에 수신
    [HttpGet]
    public async Task<IActionResult> GetProgress()
    {
        var playerId = User.GetPlayerIdRequired();
        var progress = await _tutorialService.GetProgressAsync(playerId);
        return Ok(progress);
    }

    // 튜토리얼 진행 상태 저장 — key-value upsert
    // key: URL 경로에서 수신, value: 요청 본문에서 수신
    [HttpPut("{key}")]
    public async Task<IActionResult> UpsertProgress(string key, [FromBody] TutorialUpsertRequest request)
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _tutorialService.UpsertProgressAsync(playerId, key, request.Value);

        return result.Status switch
        {
            TutorialUpsertStatus.Success => Ok(new { message = "튜토리얼 진행 상태가 저장되었습니다." }),
            // key 또는 value 길이 초과 — 클라이언트 입력 오류
            TutorialUpsertStatus.KeyTooLong => BadRequest(new { message = "키가 너무 깁니다. 최대 128자까지 허용됩니다." }),
            TutorialUpsertStatus.ValueTooLong => BadRequest(new { message = "값이 너무 깁니다. 최대 512자까지 허용됩니다." }),
            // 행 상한 초과 — 서버 정책에 의한 거절
            TutorialUpsertStatus.LimitExceeded => UnprocessableEntity(new { message = "튜토리얼 진행 상태 저장 한도를 초과했습니다." }),
            _ => StatusCode(500, new { message = "알 수 없는 오류가 발생했습니다." })
        };
    }
}
