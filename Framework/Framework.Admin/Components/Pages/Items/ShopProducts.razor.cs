using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Common;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Items;

/// <summary>
/// 인게임 상점 상품 관리 페이지 코드-비하인드.
/// 상품 목록 조회, 생성, 수정, 소프트 삭제를 담당한다.
/// </summary>
public partial class ShopProducts : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterIsEnabled = "";   // "" = 전체, "true"/"false" = 활성/비활성

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResultDto<ShopProductItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newName = "";
    private string newDescription = "";
    private int newPriceItemId = 1;     // 기본값: Gold(ItemId=1)
    private int newPriceAmount = 100;
    private int newRewardTableId;
    private int newDailyLimit = 0;
    private int newTotalLimit = 0;
    private int? newMaxPerCall = null;  // null = 무제한 (기본값)
    private int newSortOrder = 0;
    private bool newIsEnabled = true;
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private ShopProductItem? editingProduct;
    private string editName = "";
    private string editDescription = "";
    private int editPriceItemId;
    private int editPriceAmount;
    private int editRewardTableId;
    private int editDailyLimit;
    private int editTotalLimit;
    private int? editMaxPerCall;  // null = 무제한
    private int editSortOrder;
    private bool editIsEnabled;
    private string? editError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingName = "";

    /// <summary>조회 실행 — 페이지 1로 리셋</summary>
    private async Task Search()
    {
        page = 1;
        await Load();
    }

    /// <summary>필터 초기화</summary>
    private void Reset()
    {
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

    /// <summary>상점 상품 목록 API 호출 — GET /api/admin/shop/products</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        // bool? 파싱 — 빈 문자열은 null(전체 조회)로 처리
        bool? enabledBool = bool.TryParse(filterIsEnabled, out var e) ? e : (bool?)null;

        var url = ApiRoutes.AdminShopProducts.Search(enabledBool, page, pageSize);
        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResultDto<ShopProductItem>>(AdminJsonOptions.Default);
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newName = "";
        newDescription = "";
        newPriceItemId = 1;
        newPriceAmount = 100;
        newRewardTableId = 0;
        newDailyLimit = 0;
        newTotalLimit = 0;
        newMaxPerCall = null;
        newSortOrder = 0;
        newIsEnabled = true;
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal() => showCreateModal = false;

    /// <summary>상점 상품 생성 — POST /api/admin/shop/products</summary>
    private async Task Create()
    {
        createError = null;

        // 필수 필드 검증
        if (string.IsNullOrWhiteSpace(newName))
        {
            createError = "상품명을 입력해주세요.";
            return;
        }
        if (newPriceItemId < 1)
        {
            createError = "가격 재화 ItemId를 1 이상으로 입력해주세요.";
            return;
        }
        if (newPriceAmount < 1)
        {
            createError = "가격 수량을 1 이상으로 입력해주세요.";
            return;
        }
        if (newRewardTableId < 1)
        {
            createError = "RewardTableId를 1 이상으로 입력해주세요.";
            return;
        }

        var payload = new
        {
            Name = newName.Trim(),
            Description = newDescription.Trim(),
            PriceItemId = newPriceItemId,
            PriceAmount = newPriceAmount,
            RewardTableId = newRewardTableId,
            DailyLimit = newDailyLimit,
            TotalLimit = newTotalLimit,
            // MaxPerCall — null이면 무제한, 입력값이 있으면 1회 최대 수량
            MaxPerCall = newMaxPerCall,
            IsEnabled = newIsEnabled,
            SortOrder = newSortOrder
        };
        var response = await ApiClient.PostAsync(ApiRoutes.AdminShopProducts.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"상품 '{newName}'이(가) 생성되었습니다.";
            await Load();
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>편집 모달 열기</summary>
    private void OpenEditModal(ShopProductItem product)
    {
        editingProduct = product;
        editName = product.Name;
        editDescription = product.Description;
        editPriceItemId = product.PriceItemId;
        editPriceAmount = product.PriceAmount;
        editRewardTableId = product.RewardTableId;
        editDailyLimit = product.DailyLimit;
        editTotalLimit = product.TotalLimit;
        // MaxPerCall — 기존 값 그대로 바인딩 (null이면 무제한 표시)
        editMaxPerCall = product.MaxPerCall;
        editSortOrder = product.SortOrder;
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

        var payload = new
        {
            Name = editName.Trim(),
            Description = editDescription.Trim(),
            PriceItemId = editPriceItemId,
            PriceAmount = editPriceAmount,
            RewardTableId = editRewardTableId,
            DailyLimit = editDailyLimit,
            TotalLimit = editTotalLimit,
            // MaxPerCall — null이면 변경 없음, 값 있으면 해당 값으로 업데이트
            MaxPerCall = editMaxPerCall,
            IsEnabled = editIsEnabled,
            SortOrder = editSortOrder
        };
        var response = await ApiClient.PutAsync(ApiRoutes.AdminShopProducts.ById(editingProduct.Id), payload);

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
    private void OpenDeleteModal(ShopProductItem p)
    {
        deletingId = p.Id;
        deletingName = p.Name;
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete() => showDeleteModal = false;

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var response = await ApiClient.DeleteAsync(ApiRoutes.AdminShopProducts.ById(deletingId));

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
        deletingName = "";
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 상점 상품 목록 응답 DTO — AdminJsonOptions.Default로 역직렬화
    private record ShopProductItem(
        int Id,
        string Name,
        string Description,
        int PriceItemId,
        string PriceItemName,
        int PriceAmount,
        int RewardTableId,
        int DailyLimit,
        int TotalLimit,
        bool IsEnabled,
        int SortOrder,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int? MaxPerCall = null  // 1회 최대 구매 수량 — null이면 무제한
    );
}
