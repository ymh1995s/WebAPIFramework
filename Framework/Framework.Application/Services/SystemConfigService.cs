using Framework.Application.Interfaces;
using Framework.Domain.Constants;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

// 시스템 설정 서비스 구현체
public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _repository;

    public SystemConfigService(ISystemConfigRepository repository)
    {
        _repository = repository;
    }

    // DB에서 일일 보상 활성화 여부 조회 ("true" 문자열과 비교)
    public async Task<bool> GetDailyRewardEnabledAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyLoginRewardEnabled);
        return value == "true";
    }

    // 일일 보상 활성화 여부를 DB에 저장
    public async Task SetDailyRewardEnabledAsync(bool enabled)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyLoginRewardEnabled, enabled ? "true" : "false");
        await _repository.SaveChangesAsync();
    }

    // DB에서 점검 모드 활성화 여부 조회 (수동)
    public async Task<bool> GetMaintenanceModeAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceMode);
        return value == "true";
    }

    // 점검 모드 활성화 여부를 DB에 저장 (수동)
    public async Task SetMaintenanceModeAsync(bool enabled)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceMode, enabled ? "true" : "false");
        await _repository.SaveChangesAsync();
    }

    // 점검 예약 시작 시각 조회 (없으면 null)
    public async Task<DateTime?> GetMaintenanceStartAtAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceStartAt);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    // 점검 예약 시작 시각 저장 (null이면 삭제)
    public async Task SetMaintenanceStartAtAsync(DateTime? dateTime)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceStartAt, dateTime?.ToString("O") ?? "");
        await _repository.SaveChangesAsync();
    }

    // 점검 예약 종료 시각 조회 (없으면 null)
    public async Task<DateTime?> GetMaintenanceEndAtAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceEndAt);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    // 점검 예약 종료 시각 저장 (null이면 삭제)
    public async Task SetMaintenanceEndAtAsync(DateTime? dateTime)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceEndAt, dateTime?.ToString("O") ?? "");
        await _repository.SaveChangesAsync();
    }

    // 클라이언트 앱 강제 업데이트 기준 최소 버전 조회 (미설정 시 "0.0.0" — 강제 업데이트 없음)
    // 서버 버전이 아닌 앱스토어에 배포된 Unity 클라이언트 빌드 버전 기준
    public async Task<string> GetClientAppMinVersionAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.ClientAppMinVersion);
        return string.IsNullOrEmpty(value) ? "0.0.0" : value;
    }

    // 클라이언트 앱 강제 업데이트 기준 최소 버전 저장
    public async Task SetClientAppMinVersionAsync(string version)
    {
        await _repository.SetValueAsync(SystemConfigKeys.ClientAppMinVersion, version);
        await _repository.SaveChangesAsync();
    }

    // 앱스토어에 배포된 클라이언트 앱 최신 버전 조회 (미설정 시 빈 문자열)
    public async Task<string> GetClientAppLatestVersionAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.ClientAppLatestVersion);
        return value ?? "";
    }

    // 앱스토어에 배포된 클라이언트 앱 최신 버전 저장
    public async Task SetClientAppLatestVersionAsync(string version)
    {
        await _repository.SetValueAsync(SystemConfigKeys.ClientAppLatestVersion, version);
        await _repository.SaveChangesAsync();
    }

    // 수동 ON 또는 현재 시각이 예약 범위 안에 있으면 점검 중으로 판단
    public async Task<bool> IsUnderMaintenanceAsync()
    {
        if (await GetMaintenanceModeAsync()) return true;

        var start = await GetMaintenanceStartAtAsync();
        var end = await GetMaintenanceEndAtAsync();
        var now = DateTime.UtcNow;

        return start.HasValue && end.HasValue && now >= start.Value && now <= end.Value;
    }
}
