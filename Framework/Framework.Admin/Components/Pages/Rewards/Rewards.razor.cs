using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 관리 페이지 코드-비하인드.
/// 하루 기준 시각, 월 28회 초과 기본 보상, 수동 우편 발송을 담당한다.
/// </summary>
public partial class Rewards : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 아이템 목록 (수동 발송 드롭다운) ─────────────
    private List<ItemOption> itemList = new();

    // ─── 하루 기준 시각 설정 ───────────────────────
    // HH:mm 문자열로 바인딩 후 저장 시 시/분 분리
    private string boundaryTimeString = "00:00";
    private string? boundaryMessage;
    private bool boundarySuccess;

    // ─── 월 28회 초과 기본 보상 설정 ────────────────
    // 드롭다운 바인딩용 string (빈 문자열 = 미설정)
    private string defaultItemIdStr = "";
    private int defaultItemCount;
    private string? defaultRewardMessage;
    private bool defaultRewardSuccess;

    // ─── 수동 발송 ─────────────────────────────────
    private string sendMode = "single";
    private int targetPlayerId;
    private string mailTitle = "";
    private string mailBody = "";
    /// <summary>드롭다운에서 선택된 아이템 ID (string으로 바인딩 후 파싱)</summary>
    private string? mailItemId;
    private int mailItemCount = 1;
    private int mailExpiresInDays = 30;
    private string? sendMessage;
    private bool sendSuccess;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 페이지 진입 시 기준 시각, 기본 보상, 아이템 목록 병렬 로드
            await Task.WhenAll(
                SafeExecute(LoadBoundaryAsync, msg => boundaryMessage = msg),
                SafeExecute(LoadDefaultRewardAsync, msg => defaultRewardMessage = msg),
                SafeExecute(LoadItemsAsync, msg => sendMessage = msg)
            );
            StateHasChanged();
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

    /// <summary>아이템 목록 조회 — 수동 발송 드롭다운에 사용</summary>
    private async Task LoadItemsAsync()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        itemList = await client.GetFromJsonAsync<List<ItemOption>>(ApiRoutes.AdminItems.Collection) ?? new();
    }

    /// <summary>수동 우편 발송</summary>
    private async Task SendMail()
    {
        sendMessage = null;

        if (string.IsNullOrWhiteSpace(mailTitle))
        {
            sendMessage = "제목을 입력해주세요.";
            sendSuccess = false;
            return;
        }

        // 아이템 ID 파싱 (빈 문자열이면 null)
        int? parsedItemId = int.TryParse(mailItemId, out var pid) ? pid : null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        System.Net.Http.HttpResponseMessage response;

        if (sendMode == "single")
        {
            var payload = new
            {
                PlayerId = targetPlayerId,
                Title = mailTitle,
                Body = mailBody,
                ItemId = parsedItemId,
                ItemCount = mailItemCount,
                ExpiresInDays = mailExpiresInDays
            };
            response = await client.PostAsJsonAsync(ApiRoutes.AdminMails.Single, payload);
        }
        else
        {
            var payload = new
            {
                Title = mailTitle,
                Body = mailBody,
                ItemId = parsedItemId,
                ItemCount = mailItemCount,
                ExpiresInDays = mailExpiresInDays
            };
            response = await client.PostAsJsonAsync(ApiRoutes.AdminMails.Bulk, payload);
        }

        if (response.IsSuccessStatusCode)
        {
            sendMessage = sendMode == "single" ? "발송되었습니다." : "전체 플레이어에게 발송되었습니다.";
            sendSuccess = true;
            mailTitle = "";
            mailBody = "";
            mailItemId = null;
            mailItemCount = 1;
        }
        else
        {
            sendMessage = "발송에 실패했습니다.";
            sendSuccess = false;
        }
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 아이템 드롭다운 옵션
    private record ItemOption(int Id, string Name, string Type, bool IsDeleted);

    // 기준 시각 API 응답 모델
    private record BoundaryResponse(int HourKst, int MinuteKst);

    // 기본 보상 API 응답 모델
    private record DefaultRewardResponse(int? ItemId, int ItemCount);
}
