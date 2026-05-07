using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Application.Common;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 지급 이력 조회 페이지 코드-비하인드.
/// 플레이어/SourceType/SourceKey/기간 필터 + 페이지네이션 + BundleSnapshot 상세 모달을 담당한다.
/// </summary>
public partial class RewardGrants : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private int? filterPlayerId;
    private string filterSourceType = "";
    private string filterSourceKey = "";
    private DateTime? filterFromLocal;
    private DateTime? filterToLocal;

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 50;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResultDto<RewardGrantItem>? result;
    private bool isLoading;
    private string? errorMessage;

    // ─── 스냅샷 모달 상태 ───────────────────────────
    private bool showSnapshotModal;
    private RewardGrantDetail? snapshotDetail;

    // ─── 취소 다이얼로그 상태 ──────────────────────
    private bool showCancelDialog;
    private int cancelTargetGrantId;
    private int cancelTargetPlayerId;

    // SourceType 드롭다운 옵션 — 서버 enum 값과 일치
    // DailyLogin(0)은 RewardTables 미사용이지만 이력 조회에서는 표시 유지
    private static readonly List<(string Label, int Value)> SourceTypeOptions = new()
    {
        ("DailyLogin", 0),
        ("MatchComplete", 1),
        ("QuestComplete", 2),
        ("AchievementUnlock", 3),
        ("LevelUp", 4),
        ("EventReward", 5),
        ("AdminGrant", 6),
        ("AdReward", 7),
        ("Purchase", 8),
        ("StageComplete", 9),
        ("CouponCode", 10),
        ("SeasonReward", 11),
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
        filterPlayerId = null;
        filterSourceType = "";
        filterSourceKey = "";
        filterFromLocal = null;
        filterToLocal = null;
        page = 1;
        result = null;
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

    /// <summary>API 호출 — 목록 조회</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;

        // SourceType 문자열 → int 변환
        int? sourceTypeInt = int.TryParse(filterSourceType, out var st) ? st : (int?)null;

        // 로컬 KST → UTC 변환
        var from = filterFromLocal.HasValue
            ? DateTime.SpecifyKind(filterFromLocal.Value, DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;
        var to = filterToLocal.HasValue
            ? DateTime.SpecifyKind(filterToLocal.Value, DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminRewardGrants.Search(
            filterPlayerId, sourceTypeInt, filterSourceKey, from, to, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResultDto<RewardGrantItem>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>BundleSnapshot 모달 열기 — 단건 상세 API 호출</summary>
    private async Task OpenSnapshot(int id)
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminRewardGrants.ById(id));

        if (response.IsSuccessStatusCode)
        {
            snapshotDetail = await response.Content.ReadFromJsonAsync<RewardGrantDetail>();
            showSnapshotModal = true;
        }
        else
        {
            errorMessage = "상세 조회 실패";
        }
    }

    /// <summary>스냅샷 모달 닫기</summary>
    private void CloseSnapshot()
    {
        showSnapshotModal = false;
        snapshotDetail = null;
    }

    /// <summary>취소 다이얼로그 열기 — 대상 GrantId/PlayerId 설정</summary>
    private void OpenCancelDialog(int grantId, int playerId)
    {
        cancelTargetGrantId = grantId;
        cancelTargetPlayerId = playerId;
        showCancelDialog = true;
    }

    /// <summary>취소 다이얼로그 닫기</summary>
    private void CloseCancelDialog()
    {
        showCancelDialog = false;
    }

    /// <summary>취소 완료 콜백 — 다이얼로그 닫고 목록 새로고침</summary>
    private async Task OnCancelConfirmed()
    {
        showCancelDialog = false;
        // 취소 처리 후 현재 페이지 목록을 즉시 갱신
        await Load();
    }

    /// <summary>UTC DateTime을 KST 문자열로 변환</summary>
    private static string ToKst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"))
            .ToString("yyyy-MM-dd HH:mm:ss");

    // ─── 내부 모델 ──────────────────────────────────
    // 취소 상태 및 취소 사유를 목록에서 표시하기 위해 필드 추가
    private record RewardGrantItem(int Id, int PlayerId, string SourceType, string SourceKey,
        DateTime GrantedAt, bool IsMailGrant, int? MailId,
        bool IsCancelled = false, string? CancelReason = null);

    private record RewardGrantDetail(int Id, int PlayerId, string SourceType, string SourceKey,
        DateTime GrantedAt, bool IsMailGrant, int? MailId, string BundleSnapshot,
        bool IsCancelled = false, string? CancelReason = null);

}
