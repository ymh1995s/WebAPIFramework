using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Iap;

/// <summary>
/// 인앱결제 구매 이력 조회 페이지 코드-비하인드.
/// 조회 전용 — CRUD 없음. 환불 감시 목적으로 Refunded 행 강조 처리.
/// </summary>
public partial class IapPurchases : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private int? filterPlayerId;
    private string filterStore = "";
    private string filterProductId = "";
    private string filterStatus = "";
    private DateTime? filterFrom;
    private DateTime? filterTo;

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;
    private int totalCount;

    // 총 페이지 수 계산
    private int TotalPages => pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

    // ─── 결과 상태 ──────────────────────────────────
    private List<IapPurchaseItem>? items;
    private bool isLoading;
    private string? errorMessage;

    // 스토어 드롭다운 옵션 — IapStore enum과 일치해야 함
    private static readonly List<(string Label, int Value)> StoreOptions = new()
    {
        ("Google Play", 1),
        ("Apple App Store", 2),
    };

    // 상태 드롭다운 옵션 — IapPurchaseStatus enum과 일치해야 함
    private static readonly List<(string Label, int Value)> StatusOptions = new()
    {
        ("Pending", 0),
        ("Verified", 1),
        ("Granted", 2),
        ("Refunded", 3),
        ("Failed", 4),
    };

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
        filterStore = "";
        filterProductId = "";
        filterStatus = "";
        filterFrom = null;
        filterTo = null;
        page = 1;
        items = null;
        totalCount = 0;
    }

    private async Task PrevPage()
    {
        if (page <= 1) return;
        page--;
        await Load();
    }

    private async Task NextPage()
    {
        if (page >= TotalPages) return;
        page++;
        await Load();
    }

    /// <summary>구매 이력 목록 API 호출 — GET /api/admin/iap/purchases</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;

        // 필터 값 파싱 — 빈 문자열은 null로 처리
        int? storeInt  = int.TryParse(filterStore,  out var s) ? s : (int?)null;
        int? statusInt = int.TryParse(filterStatus, out var st) ? st : (int?)null;
        string? productIdFilter = string.IsNullOrWhiteSpace(filterProductId) ? null : filterProductId.Trim();

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminIapPurchases.Search(
            filterPlayerId, storeInt, productIdFilter, statusInt,
            filterFrom, filterTo, page, pageSize);

        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            // API 응답 구조: { items: [...], total: N }
            var raw = await response.Content.ReadFromJsonAsync<PurchaseSearchResult>();
            items = raw?.Items ?? new();
            totalCount = raw?.Total ?? 0;
        }
        else
        {
            errorMessage = $"조회 실패: {response.StatusCode}";
            items = null;
            totalCount = 0;
        }

        isLoading = false;
    }

    // ─── 내부 모델 ──────────────────────────────────

    // API 응답 래퍼 — { items, total } 구조
    private record PurchaseSearchResult(List<IapPurchaseItem> Items, int Total);

    // 구매 이력 응답 DTO — IapPurchaseDto 구조 반영
    // API가 enum을 int로 반환하므로 Store/Status는 int로 받아 표시 시 변환
    private record IapPurchaseItem(
        int Id,
        int PlayerId,
        int Store,              // IapStore enum int값 (1=Google, 2=Apple)
        string ProductId,
        string PurchaseToken,
        string? OrderId,
        int Status,             // IapPurchaseStatus enum int값
        DateTime? PurchaseTimeUtc,
        DateTime? GrantedAt,
        DateTime? RefundedAt,
        string? FailureReason,
        DateTime CreatedAt
    )
    {
        // 표시용 스토어 이름
        public string StoreLabel => Store switch { 1 => "Google", 2 => "Apple", _ => Store.ToString() };

        // PurchaseToken 앞 20자 + 말줄임 — 긴 토큰을 테이블에 축약 표시
        public string PurchaseTokenShort => PurchaseToken.Length > 20
            ? PurchaseToken[..20] + "..."
            : PurchaseToken;

        // StatusValue 별칭 — razor에서 int로 비교할 때 사용
        public int StatusValue => Status;
    };
}
