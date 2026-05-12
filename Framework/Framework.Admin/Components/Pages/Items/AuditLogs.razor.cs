using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Common;
using Framework.Domain.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Items;

/// <summary>
/// 아이템 변동 로그(감사 로그) 조회 페이지 코드-비하인드.
/// 플레이어/아이템/기간/이상치 필터를 적용한 페이지네이션 조회를 담당한다.
/// </summary>
public partial class AuditLogs : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // 필터 상태
    private int? filterPlayerId;
    private int? filterItemId;
    private DateTime? filterFromLocal;
    private DateTime? filterToLocal;
    private string filterAnomaly = "";

    // 페이지네이션 상태
    private int page = 1;
    private int pageSize = 50;

    // 결과 상태
    private PagedResultDto<AuditLogDto>? result;
    private bool isLoading;
    private string? errorMessage;

    /// <summary>조회 실행 — 페이지 1로 리셋</summary>
    private async Task Search()
    {
        page = 1;
        await Load();
    }

    /// <summary>필터 초기화</summary>
    private void Reset()
    {
        filterPlayerId = null;
        filterItemId = null;
        filterFromLocal = null;
        filterToLocal = null;
        filterAnomaly = "";
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

    /// <summary>실제 API 호출 수행</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;

        bool? anomaly = filterAnomaly switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };

        // 로컬 시간 입력 → UTC 변환 후 API 전송
        var from = filterFromLocal.HasValue ? DateTime.SpecifyKind(filterFromLocal.Value, DateTimeKind.Local).ToUniversalTime() : (DateTime?)null;
        var to = filterToLocal.HasValue ? DateTime.SpecifyKind(filterToLocal.Value, DateTimeKind.Local).ToUniversalTime() : (DateTime?)null;

        var url = ApiRoutes.AdminAuditLogs.Search(filterPlayerId, filterItemId, from, to, anomaly, page, pageSize);
        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResultDto<AuditLogDto>>(AdminJsonOptions.Default);
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>UTC DateTime을 KST 문자열로 변환</summary>
    private static string ToKst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"))
            .ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>현재 적용된 필터를 (라벨, 값) 튜플 목록으로 반환 — 결과 상단 요약 표시용</summary>
    private IEnumerable<(string Label, string Value)> GetActiveFilters()
    {
        if (filterPlayerId.HasValue) yield return ("플레이어", filterPlayerId.Value.ToString());
        if (filterItemId.HasValue) yield return ("아이템", filterItemId.Value.ToString());
        if (filterFromLocal.HasValue) yield return ("시작", filterFromLocal.Value.ToString("yyyy-MM-dd HH:mm"));
        if (filterToLocal.HasValue) yield return ("종료", filterToLocal.Value.ToString("yyyy-MM-dd HH:mm"));
        if (filterAnomaly == "true") yield return ("이상치", "이상치만");
        else if (filterAnomaly == "false") yield return ("이상치", "정상만");
        yield return ("페이지 크기", pageSize.ToString());
    }

    /// <summary>사유(Reason) 값에 따른 Bootstrap 배지 색상 클래스 반환 — 현재 구현된 값: MailClaim, ItemUse</summary>
    private static string ReasonBadgeClass(string reason) => reason switch
    {
        // 현재 구현된 사유 값
        AuditLogReasons.MailClaim => "bg-primary",
        AuditLogReasons.ItemUse   => "bg-info",
        // AuditLog.Reason에는 미정의이지만 RewardSourceType.AdminGrant 매핑용 배지 색상 (운영 지급 표시)
        "AdminGrant"   => "bg-warning text-dark",
        "ShopPurchase" => "bg-success",
        _              => "bg-secondary"
    };

    // API 응답 매핑용 로컬 DTO
    private record AuditLogDto(long Id, int PlayerId, int ItemId, string ItemName, string Reason, int ChangeAmount, int BalanceBefore, int BalanceAfter, bool IsAnomaly, DateTime CreatedAt);
}
