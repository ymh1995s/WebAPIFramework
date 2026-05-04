using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 보안 감시 페이지 코드-비하인드.
/// 탭 1: 통합 타임라인 (Rate Limit / 이상치 / 밴)
/// 탭 2: IP 집계 (기존 Rate Limit 뷰 유지)
/// </summary>
public partial class RateLimitLogs : SafeComponentBase
{
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Rate Limit 설정값 — API(GET /api/admin/security/rate-limit-config)에서 동적으로 읽어옴
    // 원천: 서버 appsettings.json 의 RateLimiting 섹션 (auth 정책은 인증 여부에 따라 분기)
    // 미인증(IP) 기준 한도 — 서버 appsettings.json: RateLimiting:AuthPermitLimit
    private int authPermitLimit = 0;
    // 인증(PlayerId) 기준 한도 — 서버 appsettings.json: RateLimiting:AuthPlayerPermitLimit
    private int authPlayerPermitLimit = 0;

    // 현재 활성 탭 — "timeline" 또는 "ip"
    private string activeTab = "timeline";

    // ── 통합 타임라인 상태 ──────────────────────────────────────────────────
    private bool isTimelineLoading;
    private List<SecurityTimelineItemDto>? timelineItems;
    private string? timelineError;

    // 밴 처리 중인 PlayerId 집합 — 더블클릭 방지용 행 단위 락
    private HashSet<int> banningPlayerIds = [];

    // 타임라인 필터
    private DateTime? filterFrom = DateTime.Today.AddDays(-7);
    private DateTime? filterTo   = DateTime.Today;
    private int?      filterPlayerId;
    private string?   filterIp;

    // ── IP 집계 상태 (기존) ────────────────────────────────────────────────
    private bool isLoading;
    private List<RateLimitLogDto>? logs;
    private string? loadError;

    // 침투 시도 상태
    private bool isAttacking;
    private List<AttackResult> attackResults = [];

    // UTC → KST 변환 헬퍼
    private static DateTime ToKst(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

    /// <summary>페이지 초기화 시 Rate Limit 설정값 로드</summary>
    protected override async Task OnInitializedAsync()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminSecurity.RateLimitConfig);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<RateLimitConfigDto>();
            if (data is not null)
            {
                // auth 정책의 IP 한도·PlayerId 한도를 각각 바인딩
                authPermitLimit = data.AuthPermitLimit;
                authPlayerPermitLimit = data.AuthPlayerPermitLimit;
            }
        }
    }

    /// <summary>보안 통합 타임라인 조회</summary>
    private async Task LoadTimeline()
    {
        isTimelineLoading = true;
        timelineError = null;

        var url = ApiRoutes.AdminSecurity.Timeline(
            filterFrom?.ToUniversalTime(),
            filterTo?.AddDays(1).ToUniversalTime(), // 종료일 당일 포함
            filterPlayerId,
            filterIp);

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            timelineItems = await response.Content.ReadFromJsonAsync<List<SecurityTimelineItemDto>>();
        else
            timelineError = $"조회 실패: {response.StatusCode}";

        isTimelineLoading = false;
    }

    /// <summary>IP별 Rate Limit 집계 조회 (기존)</summary>
    private async Task LoadLogs()
    {
        isLoading = true;
        loadError = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminRateLimitLogs.Collection);

        if (response.IsSuccessStatusCode)
            logs = await response.Content.ReadFromJsonAsync<List<RateLimitLogDto>>();
        else
            loadError = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>
    /// 침투 시도 테스트 — auth 엔드포인트에 Rate Limit 한도+5회 연속 요청하여 429 유도.
    /// authPermitLimit이 0이면 기본값 65 사용.
    /// </summary>
    private async Task RunAttackTest()
    {
        isAttacking = true;
        attackResults = [];

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { DeviceId = "attack-test-device" };

        // authPermitLimit이 아직 로드되지 않았거나 0이면 기본값 65 사용
        var attackCount = authPermitLimit > 0 ? authPermitLimit + 5 : 65;

        for (var i = 1; i <= attackCount; i++)
        {
            var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Guest, payload);
            var status = (int)response.StatusCode;
            attackResults.Add(new AttackResult(i, status, status == 429 ? "Too Many Requests" : "OK"));
            StateHasChanged();
        }

        isAttacking = false;
        await LoadLogs();
    }

    /// <summary>타임라인에서 직접 영구밴 처리 — JS confirm으로 이중 확인</summary>
    private async Task BanFromTimeline(int targetPlayerId)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"PlayerId {targetPlayerId}를 영구밴 처리하시겠습니까?");
        if (!confirmed) return;

        banningPlayerIds.Add(targetPlayerId);
        timelineError = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PostAsJsonAsync(
            ApiRoutes.AdminPlayers.Ban(targetPlayerId),
            new { BannedUntil = (DateTime?)null });

        banningPlayerIds.Remove(targetPlayerId);

        if (response.IsSuccessStatusCode)
            await LoadTimeline(); // 밴 성공 시 타임라인 갱신
        else
            timelineError = $"밴 처리 실패: {response.StatusCode}";
    }

    // Rate Limit 설정 응답 DTO — auth 정책의 IP/PlayerId 한도를 각각 포함
    // API 엔드포인트 GET /api/admin/security/rate-limit-config 의 응답 매핑용
    // 두 값 모두 서버 appsettings.json 의 RateLimiting 섹션에서 동적으로 읽혀 옴
    private record RateLimitConfigDto(
        // 미인증(IP) 기준 분당 허용 횟수 — 서버 appsettings.json 의 RateLimiting:AuthPermitLimit 에서 읽힘
        int AuthPermitLimit,
        // 인증(PlayerId) 기준 분당 허용 횟수 — 서버 appsettings.json 의 RateLimiting:AuthPlayerPermitLimit 에서 읽힘
        int AuthPlayerPermitLimit
    );

    // API 응답 매핑용 로컬 DTO
    private record RateLimitLogDto(string IpAddress, int Count, DateTime LastOccurredAt);
    private record AttackResult(int Index, int Status, string Label);
    private record SecurityTimelineItemDto(
        DateTime OccurredAt,
        string Type,
        int? PlayerId,
        string? IpAddress,
        string Description,
        string Severity,
        bool IsBanned);
}
