using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

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

    // 클라이언트 로그인 시 호출 - 오늘 보상 미수령 시 우편 자동 발송
    [HttpPost("{playerId}")]
    public async Task<IActionResult> ProcessLogin(int playerId)
    {
        var rewarded = await _dailyLoginService.ProcessLoginRewardAsync(playerId);
        return Ok(new { Rewarded = rewarded });
    }
}
