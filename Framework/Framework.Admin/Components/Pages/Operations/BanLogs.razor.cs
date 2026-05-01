using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

// 밴/밴해제 이력 페이지 코드-비하인드
// URL 쿼리스트링으로 playerId 초기값 수신 가능 (/ban-logs?playerId=123)
public partial class BanLogs : SafeComponentBase
{
    // Admin API 호출용 HttpClient
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // URL 쿼리스트링에서 초기 PlayerId 수신 — PlayerManagement 페이지의 "밴 이력" 링크 연동
    [SupplyParameterFromQuery(Name = "playerId")]
    private int? InitialPlayerId { get; set; }

    // ── 필터 상태 ──────────────────────────────────────────────
    private int?     filterPlayerId;       // PlayerId 필터
    private string   filterAction  = "";   // 액션 필터 ("" / "1" / "2")
    private DateTime? filterFrom;          // 시작일 필터
    private DateTime? filterTo;            // 종료일 필터

    // ── 페이지 상태 ────────────────────────────────────────────
    private const int PageSize = 20;
    private int currentPage = 1;

    // ── 로딩/결과/오류 상태 ───────────────────────────────────
    private bool isLoading;
    private BanLogPagedResult? result;
    private string? error;

    // 전체 페이지 수 계산
    private int TotalPages => result is null || result.TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)result.TotalCount / PageSize);

    // UTC → KST 변환 헬퍼
    private static DateTime ToKst(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

    // 페이지 초기화 — URL 쿼리스트링 PlayerId가 있으면 필터에 세팅 후 자동 조회
    protected override async Task OnInitializedAsync()
    {
        if (InitialPlayerId.HasValue)
        {
            filterPlayerId = InitialPlayerId.Value;
            await Search();
        }
    }

    // 조회 버튼 클릭 — 첫 페이지부터 검색
    private async Task Search()
    {
        currentPage = 1;
        await LoadPage();
    }

    // 이전 페이지 이동
    private async Task PrevPage()
    {
        if (currentPage > 1) { currentPage--; await LoadPage(); }
    }

    // 다음 페이지 이동
    private async Task NextPage()
    {
        if (currentPage < TotalPages) { currentPage++; await LoadPage(); }
    }

    // 필터 초기화 — 상태 리셋 후 결과 제거 (재조회 없음)
    private void Reset()
    {
        filterPlayerId = null;
        filterAction   = "";
        filterFrom     = null;
        filterTo       = null;
        currentPage    = 1;
        result         = null;
        error          = null;
    }

    // 실제 API 호출 — 필터 + 페이지 파라미터로 검색
    private async Task LoadPage()
    {
        isLoading = true;
        error     = null;

        // filterAction 문자열 → BanAction? enum 변환
        BanAction? action = filterAction switch
        {
            "1" => BanAction.Ban,
            "2" => BanAction.Unban,
            _   => null
        };

        // 종료일은 당일 포함을 위해 다음날 00:00 UTC로 변환
        var toUtc = filterTo?.AddDays(1).ToUniversalTime();
        var fromUtc = filterFrom?.ToUniversalTime();

        var url = ApiRoutes.AdminBanLogs.Search(filterPlayerId, action, fromUtc, toUtc, currentPage, PageSize);
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<BanLogPagedResult>();
        else
            error = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    // ── API 응답 매핑용 로컬 DTO ──────────────────────────────

    // BanLogDto 응답 구조 — AdminBanLogsController 반환 형태와 일치
    private record BanLogItem(
        long Id,
        int PlayerId,
        string? PlayerNickname,
        BanAction Action,
        DateTime? BannedUntil,
        string? Reason,
        int ActorType,
        int? ActorId,
        string? ActorIp,
        DateTime CreatedAt);

    // BanLogPagedDto 응답 래퍼
    private record BanLogPagedResult(
        List<BanLogItem> Items,
        int TotalCount,
        int Page,
        int PageSize);
}
