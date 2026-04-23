using Framework.Api.Filters;
using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

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

    // 점검 모드 활성화 여부 조회 (수동)
    [HttpGet("maintenance-mode")]
    public async Task<IActionResult> GetMaintenanceMode()
        => Ok(new { Enabled = await _systemConfigService.GetMaintenanceModeAsync() });

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
        return Ok(new { StartAt = start, EndAt = end });
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
        => Ok(new { IsUnderMaintenance = await _systemConfigService.IsUnderMaintenanceAsync() });

    // 클라이언트 앱 버전 설정 조회 (서버 버전 아님 — 앱스토어 배포 Unity 빌드 버전 기준)
    [HttpGet("version")]
    public async Task<IActionResult> GetVersion()
    {
        var minVersion = await _systemConfigService.GetClientAppMinVersionAsync();
        var latestVersion = await _systemConfigService.GetClientAppLatestVersionAsync();
        return Ok(new { MinVersion = minVersion, LatestVersion = latestVersion });
    }

    // 클라이언트 앱 버전 설정 저장
    [HttpPut("version")]
    public async Task<IActionResult> SetVersion([FromBody] VersionConfigDto dto)
    {
        await _systemConfigService.SetClientAppMinVersionAsync(dto.MinVersion);
        await _systemConfigService.SetClientAppLatestVersionAsync(dto.LatestVersion);
        return Ok();
    }
}
