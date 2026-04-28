using Framework.Application.Features.SystemConfig;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 클라이언트 앱 버전 체크 API
// [중요] 여기서 말하는 버전은 서버 버전이 아닌 앱스토어에 배포된 Unity 클라이언트 빌드 버전임
// 서버는 기준값만 보유하고, 실제 업데이트 유도(팝업/앱스토어 이동)는 Unity 클라이언트가 처리함
// 인증 불필요 — 로그인 전 앱 실행 직후 호출
[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public VersionController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    // 클라이언트가 자신의 앱 버전을 보내면 강제 업데이트 여부와 최신 버전을 반환
    // version: Unity 클라이언트 빌드 버전 (예: "1.0.0")
    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] string version)
    {
        if (string.IsNullOrWhiteSpace(version) || !Version.TryParse(version, out var clientAppVersion))
            return BadRequest("유효하지 않은 버전 형식입니다.");

        var minVersionStr = await _systemConfigService.GetClientAppMinVersionAsync();
        var latestVersion = await _systemConfigService.GetClientAppLatestVersionAsync();

        // 최소 버전 미설정 시 강제 업데이트 없음으로 처리
        if (!Version.TryParse(minVersionStr, out var clientAppMinVersion))
            return Ok(new { IsForceUpdate = false, LatestVersion = latestVersion });

        return Ok(new
        {
            // 클라이언트 앱 버전이 최소 버전 미만이면 강제 업데이트
            IsForceUpdate = clientAppVersion < clientAppMinVersion,
            LatestVersion = latestVersion
        });
    }
}
