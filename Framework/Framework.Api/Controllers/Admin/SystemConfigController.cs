using Framework.Api.Filters;
using Framework.Application.Features.SystemConfig;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// 시스템 설정 API 컨트롤러 - Admin 전용 (X-Admin-Key 헤더 필요)
// Blazor에서 api/admin/systemconfig 경로로 호출하므로 Route 경로 일치 필요
[AdminApiKey]
[ApiController]
[Route("api/admin/systemconfig")]
public class SystemConfigController : ControllerBase
{
    private readonly ISystemConfigService _systemConfigService;

    public SystemConfigController(ISystemConfigService systemConfigService)
    {
        _systemConfigService = systemConfigService;
    }

    // 점검 모드 활성화 여부 조회 (수동)
    [HttpGet("maintenance-mode")]
    public async Task<IActionResult> GetMaintenanceMode()
        => Ok(new MaintenanceModeResponse(await _systemConfigService.GetMaintenanceModeAsync()));

    // 점검 모드 활성화 여부 변경 (수동)
    [HttpPut("maintenance-mode")]
    public async Task<IActionResult> SetMaintenanceMode([FromBody] bool enabled)
    {
        await _systemConfigService.SetMaintenanceModeAsync(enabled);
        return Ok();
    }

    // 점검 예약 시작/종료 시각 조회
    [HttpGet("maintenance-schedule")]
    public async Task<IActionResult> GetMaintenanceSchedule()
    {
        var start = await _systemConfigService.GetMaintenanceStartAtAsync();
        var end = await _systemConfigService.GetMaintenanceEndAtAsync();
        return Ok(new MaintenanceScheduleResponse(start, end));
    }

    // 점검 예약 시작/종료 시각 설정 (null이면 초기화)
    [HttpPut("maintenance-schedule")]
    public async Task<IActionResult> SetMaintenanceSchedule([FromBody] MaintenanceScheduleDto dto)
    {
        await _systemConfigService.SetMaintenanceStartAtAsync(dto.StartAt);
        await _systemConfigService.SetMaintenanceEndAtAsync(dto.EndAt);
        return Ok();
    }

    // 수동 + 예약 포함 현재 실제 점검 여부 조회
    [HttpGet("maintenance-status")]
    public async Task<IActionResult> GetMaintenanceStatus()
        => Ok(new MaintenanceStatusResponse(await _systemConfigService.IsUnderMaintenanceAsync()));

    // 클라이언트 앱 버전 설정 조회 (서버 버전 아님 — 앱스토어 배포 Unity 빌드 버전 기준)
    [HttpGet("version")]
    public async Task<IActionResult> GetVersion()
    {
        var minVersion = await _systemConfigService.GetClientAppMinVersionAsync();
        var latestVersion = await _systemConfigService.GetClientAppLatestVersionAsync();
        return Ok(new VersionConfigResponse(minVersion, latestVersion));
    }

    // 클라이언트 앱 버전 설정 저장
    [HttpPut("version")]
    public async Task<IActionResult> SetVersion([FromBody] VersionConfigDto dto)
    {
        await _systemConfigService.SetClientAppMinVersionAsync(dto.MinVersion);
        await _systemConfigService.SetClientAppLatestVersionAsync(dto.LatestVersion);
        return Ok();
    }

    // 일일 보상 하루 기준 시각 조회 (KST 시/분)
    [HttpGet("daily-reward-day-boundary")]
    public async Task<IActionResult> GetDailyRewardDayBoundary()
    {
        var hourKst = await _systemConfigService.GetDailyRewardDayBoundaryHourKstAsync();
        var minuteKst = await _systemConfigService.GetDailyRewardDayBoundaryMinuteKstAsync();
        return Ok(new DailyRewardDayBoundaryResponse(hourKst, minuteKst));
    }

    // 일일 보상 하루 기준 시각 저장 — hourKst: 0~23, minuteKst: 0~59
    [HttpPut("daily-reward-day-boundary")]
    public async Task<IActionResult> SetDailyRewardDayBoundary([FromBody] DailyRewardDayBoundaryDto dto)
    {
        // 유효 범위 검증
        if (dto.HourKst < 0 || dto.HourKst > 23)
            return BadRequest("hourKst는 0~23 사이여야 합니다.");
        if (dto.MinuteKst < 0 || dto.MinuteKst > 59)
            return BadRequest("minuteKst는 0~59 사이여야 합니다.");

        await _systemConfigService.SetDailyRewardDayBoundaryHourKstAsync(dto.HourKst);
        await _systemConfigService.SetDailyRewardDayBoundaryMinuteKstAsync(dto.MinuteKst);
        return Ok();
    }

    // 월 28회 초과 시 지급할 기본 보상 설정 조회
    [HttpGet("daily-reward-default")]
    public async Task<IActionResult> GetDailyRewardDefault()
    {
        var itemId = await _systemConfigService.GetDailyRewardDefaultItemIdAsync();
        var itemCount = await _systemConfigService.GetDailyRewardDefaultItemCountAsync();
        return Ok(new DailyRewardDefaultDto(itemId, itemCount));
    }

    // 월 28회 초과 시 지급할 기본 보상 설정 저장
    [HttpPut("daily-reward-default")]
    public async Task<IActionResult> SetDailyRewardDefault([FromBody] DailyRewardDefaultDto dto)
    {
        // 수량은 음수 불가
        if (dto.ItemCount < 0)
            return BadRequest("ItemCount는 0 이상이어야 합니다.");

        await _systemConfigService.SetDailyRewardDefaultItemIdAsync(dto.ItemId);
        await _systemConfigService.SetDailyRewardDefaultItemCountAsync(dto.ItemCount);
        return Ok();
    }
}
