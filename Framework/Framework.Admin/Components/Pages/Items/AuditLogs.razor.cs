using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;

namespace Framework.Admin.Components.Pages.Items;

/// <summary>
/// 아이템 변동 로그(감사 로그) 조회 페이지 코드-비하인드.
/// 플레이어/아이템/기간/이상치 필터를 적용한 페이지네이션 조회를 담당한다.
/// </summary>
public partial class AuditLogs : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

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
    private PagedResult<AuditLogDto>? result;
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

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminAuditLogs.Search(filterPlayerId, filterItemId, from, to, anomaly, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>UTC DateTime을 KST 문자열로 변환</summary>
    private static string ToKst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"))
            .ToString("yyyy-MM-dd HH:mm:ss");

    // API 응답 매핑용 로컬 DTO
    private record AuditLogDto(long Id, int PlayerId, int ItemId, string ItemName, string Reason, int ChangeAmount, int BalanceBefore, int BalanceAfter, bool IsAnomaly, DateTime CreatedAt);
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);
}
