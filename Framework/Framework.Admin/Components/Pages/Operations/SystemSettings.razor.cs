using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 시스템 설정 페이지 코드-비하인드.
/// 수동 점검 토글, 점검 예약, 클라이언트 앱 버전 관리를 담당한다.
/// </summary>
public partial class SystemSettings : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    private bool isLoading = true;

    // 수동 점검 상태
    private bool maintenanceEnabled;
    private string? maintenanceMessage;

    // 예약 점검 상태
    private DateTime? currentStartAt;
    private DateTime? currentEndAt;
    private string? scheduleStartDate;
    private string? scheduleStartTime;
    private string? scheduleEndDate;
    private string? scheduleEndTime;
    private string? scheduleMessage;

    // 버전 설정 상태
    private string minVersion = "";
    private string latestVersion = "";
    private string? versionMessage;

    /// <summary>수동 + 예약 포함 현재 실제 점검 여부</summary>
    private bool isUnderMaintenance;

    private string? errorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadSettings, msg => errorMessage = msg);
            StateHasChanged();
        }
    }

    /// <summary>전체 설정 조회</summary>
    private async Task LoadSettings()
    {
        isLoading = true;
        errorMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");

        var r1 = await client.GetAsync(ApiRoutes.SystemConfig.MaintenanceMode);
        var r2 = await client.GetAsync(ApiRoutes.SystemConfig.MaintenanceSchedule);
        var r4 = await client.GetAsync(ApiRoutes.SystemConfig.MaintenanceStatus);
        var r5 = await client.GetAsync(ApiRoutes.SystemConfig.Version);

        if (r1.IsSuccessStatusCode && r2.IsSuccessStatusCode && r4.IsSuccessStatusCode && r5.IsSuccessStatusCode)
        {
            maintenanceEnabled = (await r1.Content.ReadFromJsonAsync<ToggleDto>())?.Enabled ?? false;
            isUnderMaintenance = (await r4.Content.ReadFromJsonAsync<StatusDto>())?.IsUnderMaintenance ?? false;

            var schedule = await r2.Content.ReadFromJsonAsync<ScheduleDto>();
            currentStartAt = schedule?.StartAt;
            currentEndAt = schedule?.EndAt;

            var version = await r5.Content.ReadFromJsonAsync<VersionDto>();
            minVersion = version?.MinVersion ?? "";
            latestVersion = version?.LatestVersion ?? "";

            // 입력 필드에 기존 예약 시각을 KST로 변환해서 날짜/시간 분리 채워둠
            if (currentStartAt.HasValue)
            {
                var local = currentStartAt.Value.ToLocalTime();
                scheduleStartDate = local.ToString("yyyy-MM-dd");
                scheduleStartTime = local.ToString("HH:mm");
            }
            if (currentEndAt.HasValue)
            {
                var local = currentEndAt.Value.ToLocalTime();
                scheduleEndDate = local.ToString("yyyy-MM-dd");
                scheduleEndTime = local.ToString("HH:mm");
            }
        }
        else
        {
            errorMessage = "설정 조회에 실패했습니다.";
        }

        isLoading = false;
    }

    /// <summary>수동 점검 토글</summary>
    private async Task ToggleMaintenance(Microsoft.AspNetCore.Components.ChangeEventArgs e)
    {
        maintenanceMessage = null;
        var enabled = (bool)(e.Value ?? false);
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.MaintenanceMode, enabled);

        if (response.IsSuccessStatusCode)
        {
            maintenanceEnabled = enabled;
            maintenanceMessage = enabled ? "점검 모드가 활성화되었습니다." : "점검 모드가 해제되었습니다.";
        }
        else
        {
            errorMessage = "점검 모드 변경에 실패했습니다.";
        }
    }

    /// <summary>점검 예약 저장 — 입력값을 UTC로 변환 후 전송</summary>
    private async Task SaveSchedule()
    {
        scheduleMessage = null;
        if (string.IsNullOrEmpty(scheduleStartDate) || string.IsNullOrEmpty(scheduleStartTime) ||
            string.IsNullOrEmpty(scheduleEndDate) || string.IsNullOrEmpty(scheduleEndTime))
        {
            errorMessage = "시작/종료 시각을 모두 입력해주세요.";
            return;
        }

        // 날짜 + 시간 문자열을 합쳐 DateTime으로 변환 후 UTC로 저장
        var scheduleStartAt = DateTime.Parse($"{scheduleStartDate}T{scheduleStartTime}");
        var scheduleEndAt = DateTime.Parse($"{scheduleEndDate}T{scheduleEndTime}");

        if (scheduleEndAt <= scheduleStartAt)
        {
            errorMessage = "종료 시각은 시작 시각보다 늦어야 합니다.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            StartAt = DateTime.SpecifyKind(scheduleStartAt, DateTimeKind.Local).ToUniversalTime(),
            EndAt = DateTime.SpecifyKind(scheduleEndAt, DateTimeKind.Local).ToUniversalTime()
        };
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.MaintenanceSchedule, payload);

        if (response.IsSuccessStatusCode)
        {
            currentStartAt = payload.StartAt;
            currentEndAt = payload.EndAt;
            scheduleMessage = "점검 예약이 저장되었습니다.";
            errorMessage = null;
        }
        else
        {
            errorMessage = "점검 예약 저장에 실패했습니다.";
        }
    }

    /// <summary>점검 예약 초기화</summary>
    private async Task ClearSchedule()
    {
        scheduleMessage = null;
        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { StartAt = (DateTime?)null, EndAt = (DateTime?)null };
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.MaintenanceSchedule, payload);

        if (response.IsSuccessStatusCode)
        {
            currentStartAt = null;
            currentEndAt = null;
            scheduleStartDate = null;
            scheduleStartTime = null;
            scheduleEndDate = null;
            scheduleEndTime = null;
            scheduleMessage = "점검 예약이 초기화되었습니다.";
            errorMessage = null;
        }
        else
        {
            errorMessage = "점검 예약 초기화에 실패했습니다.";
        }
    }

    /// <summary>버전 설정 저장</summary>
    private async Task SaveVersion()
    {
        versionMessage = null;
        if (string.IsNullOrWhiteSpace(minVersion) || string.IsNullOrWhiteSpace(latestVersion))
        {
            errorMessage = "최소 버전과 최신 버전을 모두 입력해주세요.";
            return;
        }

        // 버전 형식 유효성 검사 (예: 1.0.0)
        if (!Version.TryParse(minVersion, out _) || !Version.TryParse(latestVersion, out _))
        {
            errorMessage = "버전 형식이 올바르지 않습니다. 예: 1.0.0";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.Version, new { MinVersion = minVersion, LatestVersion = latestVersion });

        if (response.IsSuccessStatusCode)
        {
            versionMessage = "버전 설정이 저장되었습니다.";
            errorMessage = null;
        }
        else
        {
            errorMessage = "버전 설정 저장에 실패했습니다.";
        }
    }

    // API 응답 매핑용 로컬 DTO
    private record ToggleDto(bool Enabled);
    private record StatusDto(bool IsUnderMaintenance);
    private record ScheduleDto(DateTime? StartAt, DateTime? EndAt);
    private record VersionDto(string MinVersion, string LatestVersion);
}
