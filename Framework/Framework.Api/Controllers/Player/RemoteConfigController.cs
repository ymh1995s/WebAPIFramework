using Framework.Api.Constants;
using Framework.Api.Responses;
using Framework.Application.Features.SystemConfig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// RemoteConfig API — Unity 클라이언트가 앱 실행 초기에 서버에서 설정값을 내려받는 엔드포인트
// 로그인 전 호출 가능하도록 [AllowAnonymous] 적용
[ApiController]
[Route("api/remoteconfig")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Game)]
public class RemoteConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public RemoteConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    // "client." 접두사를 제거한 키-값 사전을 반환
    // 응답 예시: { "values": { "feature_flag": "true", "max_level": "100" } }
    [HttpGet]
    public async Task<IActionResult> GetRemoteConfig()
    {
        var values = await _systemConfigService.GetClientConfigsStrippedAsync();
        return Ok(new RemoteConfigResponse(values));
    }
}
