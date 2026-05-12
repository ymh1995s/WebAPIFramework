using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Shout;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Support;

/// <summary>
/// 1회 공지 발송 페이지 코드-비하인드.
/// Admin이 전체 또는 특정 플레이어에게 1회성 HUD 메시지를 발송하고 이력을 조회한다.
/// </summary>
public partial class Shouts : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // ─── 발송 폼 상태 ───────────────────────────────────
    // PlayerId null 또는 0이면 전체 대상
    private int? newPlayerId;
    private string newMessage = "";
    private int newExpiresInMinutes = 60;

    // 발송 결과 메시지
    private string? sendMessage;
    private bool sendSuccess;

    // ─── 이력 조회 상태 ─────────────────────────────────
    private List<ShoutAdminDto> historyItems = [];
    private bool isLoading;
    private string? historyError;

    // 이력 필터 입력값
    private int? filterPlayerId;
    private bool filterActiveOnly;

    // 페이지네이션 상태
    private int currentPage = 1;
    private const int PageSize = 20;
    private int totalCount;
    private int totalPages => totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadHistory, msg => historyError = msg);
            StateHasChanged();
        }
    }

    /// <summary>1회 공지 발송 — API에 CreateShoutDto를 POST</summary>
    private async Task SendShout()
    {
        sendMessage = null;

        // PlayerId가 0이면 null로 처리 (전체 대상)
        var targetPlayerId = (newPlayerId.HasValue && newPlayerId.Value > 0) ? newPlayerId : null;

        var payload = new
        {
            PlayerId = targetPlayerId,
            Message = newMessage,
            ExpiresInMinutes = newExpiresInMinutes
        };

        // PostAsync — AdminJsonOptions.Default로 직렬화하여 1회 공지 발송
        var response = await ApiClient.PostAsync(ApiRoutes.AdminShouts.Create, payload);

        if (response.IsSuccessStatusCode)
        {
            sendSuccess = true;
            sendMessage = "1회 공지가 발송되었습니다.";
            // 입력 초기화
            newPlayerId = null;
            newMessage = "";
            newExpiresInMinutes = 60;
            // 발송 후 이력 갱신
            await LoadHistory();
        }
        else
        {
            sendSuccess = false;
            var error = await response.Content.ReadFromJsonAsync<ErrorDto>(AdminJsonOptions.Default);
            sendMessage = error?.Message ?? $"발송 실패: {response.StatusCode}";
        }
    }

    /// <summary>발송 이력 조회 — 현재 필터/페이지 기준으로 API 호출</summary>
    private async Task LoadHistory()
    {
        isLoading = true;
        historyError = null;

        var url = ApiRoutes.AdminShouts.Search(filterPlayerId, filterActiveOnly ? true : null, currentPage, PageSize);
        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ShoutListResponse>(AdminJsonOptions.Default);
            historyItems = result?.Items ?? [];
            totalCount = result?.Total ?? 0;
        }
        else
        {
            historyError = "이력 조회에 실패했습니다.";
        }

        isLoading = false;
    }

    /// <summary>필터 조건 적용하여 이력 재조회 — 1페이지부터 시작</summary>
    private async Task SearchHistory()
    {
        currentPage = 1;
        await LoadHistory();
    }

    /// <summary>1회 공지 즉시 비활성화</summary>
    private async Task Deactivate(int id)
    {
        historyError = null;
        // PutAsync — 본문 없는 비활성화 요청 (빈 객체로 전달)
        var response = await ApiClient.PutAsync(ApiRoutes.AdminShouts.Deactivate(id), new { });

        if (response.IsSuccessStatusCode)
        {
            await LoadHistory();
        }
        else
        {
            historyError = $"비활성화 실패: {response.StatusCode}";
        }
    }

    /// <summary>이전 페이지로 이동</summary>
    private async Task PrevPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadHistory();
        }
    }

    /// <summary>다음 페이지로 이동</summary>
    private async Task NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            await LoadHistory();
        }
    }

    // ─── API 응답 역직렬화용 로컬 레코드 ───────────────

    // 에러 응답
    private record ErrorDto(string Message);
}
