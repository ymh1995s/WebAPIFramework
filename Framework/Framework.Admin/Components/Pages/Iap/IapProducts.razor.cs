using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Iap;

/// <summary>
/// 인앱결제 상품 관리 페이지 코드-비하인드.
/// 상품 목록 조회, 생성, 수정, 소프트 삭제를 담당한다.
/// </summary>
public partial class IapProducts : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase enum JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private IapStore? filterStore;           // null = 전체
    private IapProductType? filterProductType; // null = 전체
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
    private IapStore? newStore;                              // null = 미선택
    private string newProductId = "";
    private IapProductType newProductType = IapProductType.Consumable; // 기본값: Consumable
    private int? newRewardTableId;
    private string newDescription = "";
    private bool newIsEnabled = true;
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private IapProductItem? editingProduct;
    private IapProductType editProductType;  // enum 타입으로 직접 바인딩
    private int? editRewardTableId;
    private string editDescription = "";
    private bool editIsEnabled;
    private string? editError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingInfo = "";

    // 스토어 드롭다운 옵션 — Domain IapStore enum 기반 (타입 안전)
    private static readonly List<(string Label, IapStore Value)> StoreOptions = new()
    {
        ("Google Play",       IapStore.Google),
        ("Apple App Store",   IapStore.Apple),
    };

    // 상품 유형 드롭다운 옵션 — Domain IapProductType enum 기반
    private static readonly List<(string Label, IapProductType Value)> ProductTypeOptions = new()
    {
        ("Consumable (소모성)",    IapProductType.Consumable),
        ("NonConsumable (비소모성)", IapProductType.NonConsumable),
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
        filterStore = null;
        filterProductType = null;
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

    /// <summary>인앱결제 상품 목록 API 호출 — enum 타입 필터를 ApiRoutes에 직접 전달</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        // bool? 파싱 — 빈 문자열은 null(전체 조회)로 처리
        bool? enabledBool = bool.TryParse(filterIsEnabled, out var e) ? e : (bool?)null;

        var url = ApiRoutes.AdminIapProducts.Search(filterStore, filterProductType, enabledBool, page, pageSize);
        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<IapProductItem>>(AdminJsonOptions.Default);
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newStore = null;
        newProductId = "";
        newProductType = IapProductType.Consumable;
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

        // 필수 필드 검증 — newStore가 null이면 미선택 상태
        if (newStore is null)
        {
            createError = "스토어를 선택해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newProductId))
        {
            createError = "ProductId(SKU)를 입력해주세요.";
            return;
        }

        // AdminJsonOptions.Default의 JsonStringEnumConverter(CamelCase)가 enum → camelCase JSON 직렬화 처리
        var payload = new
        {
            Store = newStore.Value,
            ProductId = newProductId.Trim(),
            ProductType = newProductType,
            RewardTableId = newRewardTableId,
            Description = newDescription,
            IsEnabled = newIsEnabled
        };
        var response = await ApiClient.PostAsync(ApiRoutes.AdminIapProducts.Collection, payload);

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
        editProductType = product.ProductType; // IapProductType enum 직접 할당
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

        // AdminJsonOptions.Default가 enum → camelCase JSON 직렬화 처리
        var payload = new
        {
            ProductType = editProductType,
            RewardTableId = editRewardTableId,
            Description = editDescription,
            IsEnabled = editIsEnabled
        };
        var response = await ApiClient.PutAsync(ApiRoutes.AdminIapProducts.ById(editingProduct.Id), payload);

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
        deletingInfo = $"[{p.StoreLabel}] {p.ProductId}";
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete() => showDeleteModal = false;

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var response = await ApiClient.DeleteAsync(ApiRoutes.AdminIapProducts.ById(deletingId));

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

    // 목록 응답 DTO — AdminJsonOptions.Default가 "google" → IapStore.Google 역직렬화
    private record IapProductItem(
        int Id,
        IapStore Store,          // Domain enum 타입으로 역직렬화
        string ProductId,
        IapProductType ProductType, // Domain enum 타입으로 역직렬화
        int? RewardTableId,
        string Description,
        bool IsEnabled,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        // 표시용 스토어 이름 변환 — enum switch로 타입 안전하게 처리
        public string StoreLabel => Store switch
        {
            IapStore.Google => "Google",
            IapStore.Apple  => "Apple",
            _               => Store.ToString()
        };

        // 표시용 상품 유형 이름 변환 — enum switch로 타입 안전하게 처리
        public string ProductTypeLabel => ProductType switch
        {
            IapProductType.Consumable    => "Consumable",
            IapProductType.NonConsumable => "NonConsumable",
            _                            => ProductType.ToString()
        };
    };

    // 페이지네이션 래퍼
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
