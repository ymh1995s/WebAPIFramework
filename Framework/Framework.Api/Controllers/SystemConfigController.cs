using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 시스템 설정 API 컨트롤러 - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public SystemConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    // 일일 보상 자동 발송 활성화 여부 조회
    [HttpGet("daily-reward-enabled")]
    public async Task<IActionResult> GetDailyRewardEnabled()
        => Ok(new { Enabled = await _systemConfigService.GetDailyRewardEnabledAsync() });

    // 일일 보상 자동 발송 활성화 여부 변경
    [HttpPut("daily-reward-enabled")]
    public async Task<IActionResult> SetDailyRewardEnabled([FromBody] bool enabled)
    {
        await _systemConfigService.SetDailyRewardEnabledAsync(enabled);
        return Ok();
    }
}
