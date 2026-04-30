using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Iap;

/// <summary>
/// 인앱결제 구매 이력 조회 페이지 코드-비하인드.
/// 조회 전용 — CRUD 없음. 환불 감시 목적으로 Refunded 행 강조 처리.
/// </summary>
public partial class IapPurchases : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase enum JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private int? filterPlayerId;
    private IapStore? filterStore;           // null = 전체
    private string filterProductId = "";
    private IapPurchaseStatus? filterStatus; // null = 전체
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

    // 스토어 드롭다운 옵션 — Domain IapStore enum 기반 (타입 안전)
    private static readonly List<(string Label, IapStore Value)> StoreOptions = new()
    {
        ("Google Play",     IapStore.Google),
        ("Apple App Store", IapStore.Apple),
    };

    // 상태 드롭다운 옵션 — Domain IapPurchaseStatus enum 기반
    private static readonly List<(string Label, IapPurchaseStatus Value)> StatusOptions = new()
    {
        ("Pending",  IapPurchaseStatus.Pending),
        ("Verified", IapPurchaseStatus.Verified),
        ("Granted",  IapPurchaseStatus.Granted),
        ("Refunded", IapPurchaseStatus.Refunded),
        ("Failed",   IapPurchaseStatus.Failed),
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
        filterStore = null;
        filterProductId = "";
        filterStatus = null;
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

    /// <summary>구매 이력 목록 API 호출 — enum 타입 필터를 ApiRoutes에 직접 전달</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;

        // ProductId 빈 문자열은 null로 처리 (전체 조회)
        string? productIdFilter = string.IsNullOrWhiteSpace(filterProductId) ? null : filterProductId.Trim();

        var url = ApiRoutes.AdminIapPurchases.Search(
            filterPlayerId, filterStore, productIdFilter, filterStatus,
            filterFrom, filterTo, page, pageSize);

        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
        {
            // API 응답 구조: { items: [...], total: N }
            var raw = await response.Content.ReadFromJsonAsync<PurchaseSearchResult>(AdminJsonOptions.Default);
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

    // 구매 이력 응답 DTO — AdminJsonOptions.Default가 "google" → IapStore.Google 역직렬화
    private record IapPurchaseItem(
        int Id,
        int PlayerId,
        IapStore Store,              // Domain enum 타입으로 역직렬화
        string ProductId,
        string PurchaseToken,
        string? OrderId,
        IapPurchaseStatus Status,    // Domain enum 타입으로 역직렬화
        DateTime? PurchaseTimeUtc,
        DateTime? GrantedAt,
        DateTime? RefundedAt,
        string? FailureReason,
        DateTime CreatedAt
    )
    {
        // 표시용 스토어 이름 변환 — enum switch로 타입 안전하게 처리
        public string StoreLabel => Store switch
        {
            IapStore.Google => "Google",
            IapStore.Apple  => "Apple",
            _               => Store.ToString()
        };

        // PurchaseToken 앞 20자 + 말줄임 — 긴 토큰을 테이블에 축약 표시
        public string PurchaseTokenShort => PurchaseToken.Length > 20
            ? PurchaseToken[..20] + "..."
            : PurchaseToken;

        // 환불 여부 확인용 — razor에서 행 강조 처리에 사용
        public bool IsRefunded => Status == IapPurchaseStatus.Refunded;
    };
}
