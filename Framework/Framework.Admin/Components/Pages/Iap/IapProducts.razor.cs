using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Iap;

/// <summary>
/// 인앱결제 상품 관리 페이지 코드-비하인드.
/// 상품 목록 조회, 생성, 수정, 소프트 삭제를 담당한다.
/// </summary>
public partial class IapProducts : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterStore = "";
    private string filterProductType = "";
    private string filterIsEnabled = "";

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResult<IapProductItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newStore = "";
    private string newProductId = "";
    private int newProductType = 1; // 기본값: Consumable
    private int? newRewardTableId;
    private string newDescription = "";
    private bool newIsEnabled = true;
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private IapProductItem? editingProduct;
    private int editProductType;
    private int? editRewardTableId;
    private string editDescription = "";
    private bool editIsEnabled;
    private string? editError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingInfo = "";

    // 스토어 드롭다운 옵션 — IapStore enum과 일치해야 함
    private static readonly List<(string Label, int Value)> StoreOptions = new()
    {
        ("Google Play", 1),
        ("Apple App Store", 2),
    };

    // 상품 유형 드롭다운 옵션 — IapProductType enum과 일치해야 함
    private static readonly List<(string Label, int Value)> ProductTypeOptions = new()
    {
        ("Consumable (소모성)", 1),
        ("NonConsumable (비소모성)", 2),
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
        filterStore = "";
        filterProductType = "";
        filterIsEnabled = "";
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

    /// <summary>인앱결제 상품 목록 API 호출</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        // 필터 값 파싱 — 빈 문자열은 null로 처리
        int? storeInt       = int.TryParse(filterStore, out var s)       ? s : (int?)null;
        int? typeInt        = int.TryParse(filterProductType, out var t) ? t : (int?)null;
        bool? enabledBool   = bool.TryParse(filterIsEnabled, out var e)  ? e : (bool?)null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminIapProducts.Search(storeInt, typeInt, enabledBool, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<IapProductItem>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newStore = "";
        newProductId = "";
        newProductType = 1;
        newRewardTableId = null;
        newDescription = "";
        newIsEnabled = true;
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal() => showCreateModal = false;

    /// <summary>인앱결제 상품 생성 — POST /api/admin/iap/products</summary>
    private async Task Create()
    {
        createError = null;

        // 필수 필드 검증
        if (string.IsNullOrWhiteSpace(newStore))
        {
            createError = "스토어를 선택해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newProductId))
        {
            createError = "ProductId(SKU)를 입력해주세요.";
            return;
        }
        if (!int.TryParse(newStore, out var storeValue))
        {
            createError = "스토어 값이 유효하지 않습니다.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            Store = storeValue,
            ProductId = newProductId.Trim(),
            ProductType = newProductType,
            RewardTableId = newRewardTableId,
            Description = newDescription,
            IsEnabled = newIsEnabled
        };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminIapProducts.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"상품 '{newProductId}'이(가) 생성되었습니다.";
            await Load();
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            createError = "동일한 Store + ProductId 조합이 이미 존재합니다.";
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>편집 모달 열기</summary>
    private void OpenEditModal(IapProductItem product)
    {
        editingProduct = product;
        editProductType = product.ProductType;
        editRewardTableId = product.RewardTableId;
        editDescription = product.Description;
        editIsEnabled = product.IsEnabled;
        editError = null;
        showEditModal = true;
    }

    /// <summary>편집 모달 닫기</summary>
    private void CloseEditModal() => showEditModal = false;

    /// <summary>상품 수정 저장 — PUT /{id}</summary>
    private async Task SaveEdit()
    {
        if (editingProduct is null) return;
        editError = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            ProductType = editProductType,
            RewardTableId = editRewardTableId,
            Description = editDescription,
            IsEnabled = editIsEnabled
        };
        var response = await client.PutAsJsonAsync(ApiRoutes.AdminIapProducts.ById(editingProduct.Id), payload);

        if (response.IsSuccessStatusCode)
        {
            showEditModal = false;
            successMessage = "상품 정보가 수정되었습니다.";
            await Load();
        }
        else
        {
            editError = $"수정 실패: {response.StatusCode}";
        }
    }

    /// <summary>삭제 확인 모달 열기</summary>
    private void OpenDeleteModal(IapProductItem p)
    {
        deletingId = p.Id;
        deletingInfo = $"[{p.Store}] {p.ProductId}";
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete() => showDeleteModal = false;

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.AdminIapProducts.ById(deletingId));

        showDeleteModal = false;

        if (response.IsSuccessStatusCode)
        {
            successMessage = "상품이 삭제되었습니다.";
            await Load();
        }
        else
        {
            errorMessage = "삭제 실패";
        }

        deletingId = 0;
        deletingInfo = "";
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 목록 응답 DTO — API 응답 역직렬화용 (IapProductDto 구조 반영)
    // API가 enum을 int로 반환하므로 Store/ProductType은 int로 받아 표시 시 변환
    private record IapProductItem(
        int Id,
        int Store,              // IapStore enum int값 (1=Google, 2=Apple)
        string ProductId,
        int ProductType,        // IapProductType enum int값 (1=Consumable, 2=NonConsumable)
        int? RewardTableId,
        string Description,
        bool IsEnabled,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        // 표시용 스토어 이름 변환
        public string StoreLabel => Store switch { 1 => "Google", 2 => "Apple", _ => Store.ToString() };
        // 표시용 상품 유형 이름 변환
        public string ProductTypeLabel => ProductType switch { 1 => "Consumable", 2 => "NonConsumable", _ => ProductType.ToString() };
    };

    // 페이지네이션 래퍼
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
