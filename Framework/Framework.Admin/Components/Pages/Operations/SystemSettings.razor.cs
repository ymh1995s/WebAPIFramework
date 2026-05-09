using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 시스템 설정 페이지 코드-비하인드.
/// 수동 점검 토글, 점검 예약, 클라이언트 앱 버전 관리,
/// 일일 보상 하루 기준 시각 및 월 28회 초과 기본 보상 설정을 담당한다.
/// (/rewards 페이지에 있던 일일 보상 설정 2개를 이 페이지로 통합)
/// </summary>
public partial class SystemSettings : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    private bool isLoading = true;

    // ─── 점검 모드 상태 ─────────────────────────────
    private bool maintenanceEnabled;
    private string? maintenanceMessage;

    // ─── 예약 점검 상태 ─────────────────────────────
    private DateTime? currentStartAt;
    private DateTime? currentEndAt;
    private string? scheduleStartDate;
    private string? scheduleStartTime;
    private string? scheduleEndDate;
    private string? scheduleEndTime;
    private string? scheduleMessage;

    // ─── 버전 설정 상태 ─────────────────────────────
    private string minVersion = "";
    private string latestVersion = "";
    private string? versionMessage;

    /// <summary>수동 + 예약 포함 현재 실제 점검 여부</summary>
    private bool isUnderMaintenance;

    // ─── 일일 보상 — 하루 기준 시각 ─────────────────
    // HH:mm 문자열로 바인딩 후 저장 시 시/분 분리
    private string boundaryTimeString = "00:00";
    private string? boundaryMessage;
    private bool boundarySuccess;

    // ─── 일일 보상 — 월 28회 초과 기본 보상 ─────────
    // 드롭다운 바인딩용 string (빈 문자열 = 미설정)
    private string defaultItemIdStr = "";
    private int defaultItemCount;
    private string? defaultRewardMessage;
    private bool defaultRewardSuccess;

    // 아이템 목록 (기본 보상 설정 드롭다운용)
    private List<ItemOption> itemList = new();

    // ─── RemoteConfig 상태 ──────────────────────────
    // 현재 등록된 클라이언트 설정 목록 (prefix 포함 원본 키)
    private List<ClientConfigItem> clientConfigs = new();
    // 새 항목 추가 입력 필드 — "client." 접두사 없는 순수 키
    private string newClientConfigKey = "";
    private string newClientConfigValue = "";
    private string? clientConfigMessage;
    private bool clientConfigSuccess;

    private string? errorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 페이지 진입 시 설정값과 일일 보상 설정, RemoteConfig를 병렬 로드
            await Task.WhenAll(
                SafeExecute(LoadSettings, msg => errorMessage = msg),
                SafeExecute(LoadBoundaryAsync, msg => boundaryMessage = msg),
                SafeExecute(LoadDefaultRewardAsync, msg => defaultRewardMessage = msg),
                SafeExecute(LoadItemsAsync, msg => errorMessage = msg),
                SafeExecute(LoadClientConfigs, msg => clientConfigMessage = msg)
            );
            StateHasChanged();
        }
    }

    /// <summary>점검/버전 전체 설정 조회</summary>
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

    /// <summary>서버에서 현재 설정된 기준 시각을 로드하여 time 인풋에 반영</summary>
    private async Task LoadBoundaryAsync()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var result = await client.GetFromJsonAsync<BoundaryResponse>(ApiRoutes.SystemConfig.DailyRewardDayBoundary);
        if (result is not null)
        {
            // HH:mm 형식 문자열로 변환하여 time 인풋 초기값 설정
            boundaryTimeString = $"{result.HourKst:D2}:{result.MinuteKst:D2}";
        }
    }

    /// <summary>time 인풋 변경 이벤트 핸들러 — ChangeEventArgs에서 string 값 추출</summary>
    private void OnBoundaryTimeChanged(Microsoft.AspNetCore.Components.ChangeEventArgs e)
    {
        boundaryTimeString = e.Value?.ToString() ?? "00:00";
    }

    /// <summary>기준 시각 저장 — HH:mm 파싱 후 PUT 요청</summary>
    private async Task SaveBoundary()
    {
        boundaryMessage = null;

        // "HH:mm" 형식 파싱
        if (!TimeOnly.TryParseExact(boundaryTimeString, "HH:mm", out var time))
        {
            boundaryMessage = "올바른 시각 형식을 입력해주세요. (예: 06:00)";
            boundarySuccess = false;
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { HourKst = time.Hour, MinuteKst = time.Minute };
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.DailyRewardDayBoundary, payload);

        if (response.IsSuccessStatusCode)
        {
            boundaryMessage = $"기준 시각이 {time.Hour:D2}:{time.Minute:D2} KST로 저장되었습니다.";
            boundarySuccess = true;
        }
        else
        {
            boundaryMessage = "저장에 실패했습니다.";
            boundarySuccess = false;
        }
    }

    /// <summary>서버에서 현재 기본 보상 설정을 로드하여 입력 필드에 반영</summary>
    private async Task LoadDefaultRewardAsync()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var result = await client.GetFromJsonAsync<DefaultRewardResponse>(ApiRoutes.SystemConfig.DailyRewardDefault);
        if (result is not null)
        {
            // 아이템 ID가 null이면 드롭다운을 "아이템 없음"으로 초기화
            defaultItemIdStr = result.ItemId.HasValue ? result.ItemId.Value.ToString() : "";
            defaultItemCount = result.ItemCount;
        }
    }

    /// <summary>기본 보상 설정 저장</summary>
    private async Task SaveDefaultReward()
    {
        defaultRewardMessage = null;

        // 드롭다운 값 파싱 (빈 문자열이면 null)
        int? parsedItemId = int.TryParse(defaultItemIdStr, out var pid) ? pid : null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { ItemId = parsedItemId, ItemCount = defaultItemCount };
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.DailyRewardDefault, payload);

        if (response.IsSuccessStatusCode)
        {
            defaultRewardMessage = "기본 보상 설정이 저장되었습니다.";
            defaultRewardSuccess = true;
        }
        else
        {
            defaultRewardMessage = "저장에 실패했습니다.";
            defaultRewardSuccess = false;
        }
    }

    /// <summary>기본 보상 설정 초기화 (아이템 없음, 수량 0으로 PUT)</summary>
    private async Task ClearDefaultReward()
    {
        defaultRewardMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { ItemId = (int?)null, ItemCount = 0 };
        var response = await client.PutAsJsonAsync(ApiRoutes.SystemConfig.DailyRewardDefault, payload);

        if (response.IsSuccessStatusCode)
        {
            defaultItemIdStr = "";
            defaultItemCount = 0;
            defaultRewardMessage = "기본 보상 설정이 초기화되었습니다.";
            defaultRewardSuccess = true;
        }
        else
        {
            defaultRewardMessage = "초기화에 실패했습니다.";
            defaultRewardSuccess = false;
        }
    }

    /// <summary>아이템 목록 조회 — 기본 보상 설정 드롭다운에 사용</summary>
    private async Task LoadItemsAsync()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        itemList = await client.GetFromJsonAsync<List<ItemOption>>(ApiRoutes.AdminItems.Collection) ?? new();
    }

    /// <summary>RemoteConfig 항목 목록 조회 — 페이지 진입 시 및 변경 후 호출</summary>
    private async Task LoadClientConfigs()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var result = await client.GetFromJsonAsync<List<ClientConfigItem>>(ApiRoutes.SystemConfig.ClientConfigs);
        clientConfigs = result ?? new();
    }

    /// <summary>클라이언트 설정 추가 또는 수정 — 입력 필드의 순수 키(prefix 없음)로 PUT 요청</summary>
    private async Task AddClientConfig()
    {
        clientConfigMessage = null;

        // 입력값 유효성 확인
        if (string.IsNullOrWhiteSpace(newClientConfigKey))
        {
            clientConfigMessage = "키를 입력해주세요.";
            clientConfigSuccess = false;
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        // 순수 키를 URL 경로에 삽입 — 서버에서 "client." 접두사 자동 부착
        var response = await client.PutAsJsonAsync(
            ApiRoutes.SystemConfig.ClientConfigByKey(newClientConfigKey),
            new { Value = newClientConfigValue }
        );

        if (response.IsSuccessStatusCode)
        {
            clientConfigMessage = $"'{newClientConfigKey}' 항목이 저장되었습니다.";
            clientConfigSuccess = true;
            newClientConfigKey = "";
            newClientConfigValue = "";
            // 저장 후 목록 갱신
            await SafeExecute(LoadClientConfigs, msg => clientConfigMessage = msg);
        }
        else
        {
            // 서버 측 키 형식 오류 등 BadRequest 메시지 표시
            var errorBody = await response.Content.ReadAsStringAsync();
            clientConfigMessage = string.IsNullOrEmpty(errorBody) ? "저장에 실패했습니다." : errorBody;
            clientConfigSuccess = false;
        }
    }

    /// <summary>클라이언트 설정 삭제 — fullKey("client." 포함)에서 접두사를 제거하여 DELETE 요청</summary>
    private async Task DeleteClientConfig(string fullKey)
    {
        clientConfigMessage = null;

        // "client." 접두사를 제거하여 순수 키 추출
        const string prefix = "client.";
        var pureKey = fullKey.StartsWith(prefix) ? fullKey[prefix.Length..] : fullKey;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.SystemConfig.ClientConfigByKey(pureKey));

        if (response.IsSuccessStatusCode)
        {
            clientConfigMessage = $"'{fullKey}' 항목이 삭제되었습니다.";
            clientConfigSuccess = true;
            // 삭제 후 목록 갱신
            await SafeExecute(LoadClientConfigs, msg => clientConfigMessage = msg);
        }
        else
        {
            clientConfigMessage = "삭제에 실패했습니다.";
            clientConfigSuccess = false;
        }
    }

    // ─── API 응답 매핑용 로컬 DTO ───────────────────
    private record ToggleDto(bool Enabled);
    private record StatusDto(bool IsUnderMaintenance);
    private record ScheduleDto(DateTime? StartAt, DateTime? EndAt);
    private record VersionDto(string MinVersion, string LatestVersion);

    // 일일 보상 관련 응답 모델
    private record BoundaryResponse(int HourKst, int MinuteKst);
    private record DefaultRewardResponse(int? ItemId, int ItemCount);

    // 아이템 드롭다운 옵션
    private record ItemOption(int Id, string Name, string Type, bool IsDeleted);

    // RemoteConfig 항목 — GET /api/admin/systemconfig/client-configs 응답 매핑
    private record ClientConfigItem(string Key, string Value);
}
