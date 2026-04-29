using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 매치 이력 조회 페이지 코드-비하인드.
/// MatchId/PlayerId/Tier/State/기간 필터 + 참가자 인라인 펼침을 담당한다.
/// </summary>
public partial class Matches : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterMatchId = "";
    private int? filterPlayerId;
    private string filterTier = "";
    private string filterState = "";
    private DateTime? filterFromLocal;
    private DateTime? filterToLocal;

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResult<MatchSummary>? result;
    private bool isLoading;
    private string? errorMessage;

    // ─── 상세 인라인 펼침 상태 ──────────────────────
    private Guid? expandedMatchId;
    private MatchDetail? matchDetail;

    // Tier 드롭다운 옵션 — Domain.Enums.Tier 정수값과 일치 (Tier1=0 ~ Tier10=9)
    private static readonly List<(string Label, int Value)> TierOptions = new()
    {
        ("Tier1", 0),
        ("Tier2", 1),
        ("Tier3", 2),
        ("Tier4", 3),
        ("Tier5", 4),
        ("Tier6", 5),
        ("Tier7", 6),
        ("Tier8", 7),
        ("Tier9", 8),
        ("Tier10", 9),
    };

    // MatchState 드롭다운 옵션 — Domain.Enums.MatchState 정수값과 일치
    private static readonly List<(string Label, int Value)> StateOptions = new()
    {
        ("Waiting", 0),
        ("InProgress", 1),
        ("Finished", 2),
        ("Aborted", 3),
    };

    /// <summary>조회 실행</summary>
    private async Task Search()
    {
        page = 1;
        await Load();
    }

    /// <summary>필터 초기화</summary>
    private void Reset()
    {
        filterMatchId = "";
        filterPlayerId = null;
        filterTier = "";
        filterState = "";
        filterFromLocal = null;
        filterToLocal = null;
        page = 1;
        result = null;
        expandedMatchId = null;
        matchDetail = null;
    }

    private async Task PrevPage()
    {
        if (page <= 1) return;
        page--;
        await Load();
    }

    private async Task NextPage()
    {
        if (result == null || page >= result.TotalPages) return;
        page++;
        await Load();
    }

    /// <summary>API 호출 — 매치 목록 조회</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;

        // Guid 파싱 (빈 문자열이면 null)
        Guid? matchGuid = Guid.TryParse(filterMatchId, out var g) ? g : (Guid?)null;
        int? tierInt = int.TryParse(filterTier, out var t) ? t : (int?)null;
        int? stateInt = int.TryParse(filterState, out var s) ? s : (int?)null;

        // 로컬 KST → UTC 변환
        var from = filterFromLocal.HasValue
            ? DateTime.SpecifyKind(filterFromLocal.Value, DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;
        var to = filterToLocal.HasValue
            ? DateTime.SpecifyKind(filterToLocal.Value, DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminMatches.Search(matchGuid, filterPlayerId, tierInt, stateInt, from, to, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<MatchSummary>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>참가자 상세 인라인 토글 — 이미 열린 매치면 닫고, 다른 매치면 API 호출 후 펼침</summary>
    private async Task ToggleDetail(Guid matchId)
    {
        if (expandedMatchId == matchId)
        {
            expandedMatchId = null;
            matchDetail = null;
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminMatches.ById(matchId));

        if (response.IsSuccessStatusCode)
        {
            matchDetail = await response.Content.ReadFromJsonAsync<MatchDetail>();
            expandedMatchId = matchId;
        }
        else
        {
            errorMessage = "매치 상세 조회 실패";
        }
    }

    /// <summary>UTC DateTime을 KST 문자열로 변환</summary>
    private static string ToKst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"))
            .ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>MatchState에 따른 배지 색상 클래스 반환</summary>
    private static string StateBadgeClass(string state) => state switch
    {
        "Waiting" => "bg-warning text-dark",
        "InProgress" => "bg-primary",
        "Finished" => "bg-success",
        "Aborted" => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>MatchOutcome에 따른 배지 색상 클래스 반환</summary>
    private static string ResultBadgeClass(string? result) => result switch
    {
        "Win" => "bg-success",
        "Lose" => "bg-danger",
        "Draw" => "bg-secondary",
        "Abandon" => "bg-warning text-dark",
        _ => "bg-secondary"
    };

    // ─── 내부 모델 ──────────────────────────────────
    private record MatchSummary(Guid Id, string Tier, string State, DateTime StartedAt, DateTime? EndedAt, int ParticipantCount);
    private record MatchParticipant(int Id, int PlayerId, string Nickname, string HumanType, int? Score, string? Result);
    private record MatchDetail(Guid Id, string Tier, string State, DateTime StartedAt, DateTime? EndedAt, List<MatchParticipant> Participants);

    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
