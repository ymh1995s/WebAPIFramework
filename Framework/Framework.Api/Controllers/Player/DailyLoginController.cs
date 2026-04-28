using Framework.Application.Features.DailyLogin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 일일 로그인 보상 API 컨트롤러 - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DailyLoginController : ControllerBase
{
    private readonly IDailyLoginService _dailyLoginService;

    public DailyLoginController(IDailyLoginService dailyLoginService)
    {
        _dailyLoginService = dailyLoginService;
    }

    // 클라이언트 로그인 시 호출 - JWT에서 추출한 본인 PlayerId 기준으로 오늘 보상 미수령 시 우편 자동 발송
    // 과거에는 URL에서 playerId를 받았으나, 타인 트리거 방지를 위해 JWT claim만 신뢰하도록 변경
    [HttpPost]
    public async Task<IActionResult> ProcessLogin()
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var rewarded = await _dailyLoginService.ProcessLoginRewardAsync(playerId);
        return Ok(new { Rewarded = rewarded });
    }
}
