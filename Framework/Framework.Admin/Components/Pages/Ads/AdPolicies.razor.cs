using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Ads;

/// <summary>
/// 광고 정책 관리 페이지 코드-비하인드.
/// 목록 조회, 생성, 수정, 소프트 삭제를 담당한다.
/// </summary>
public partial class AdPolicies : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterNetwork = "";

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResult<AdPolicyItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newNetwork = "";
    private string newPlacementId = "";
    private int newPlacementType = 1;
    private int? newRewardTableId;
    private int newDailyLimit = 0;
    private bool newIsEnabled = true;
    private string newDescription = "";
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private AdPolicyItem? editingPolicy;
    private int? editRewardTableId;
    private int editDailyLimit;
    private bool editIsEnabled;
    private string editDescription = "";
    private string? editError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingInfo = "";

    // 광고 네트워크 드롭다운 옵션 — AdNetworkType enum과 일치해야 함
    private static readonly List<(string Label, int Value)> NetworkOptions = new()
    {
        ("UnityAds", 1),
        ("IronSource", 2),
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
        filterNetwork = "";
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

    /// <summary>광고 정책 목록 API 호출</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        int? networkInt = int.TryParse(filterNetwork, out var n) ? n : (int?)null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminAdPolicies.Search(networkInt, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<AdPolicyItem>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newNetwork = "";
        newPlacementId = "";
        newPlacementType = 1;
        newRewardTableId = null;
        newDailyLimit = 0;
        newIsEnabled = true;
        newDescription = "";
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal() => showCreateModal = false;

    /// <summary>광고 정책 생성 — POST /</summary>
    private async Task Create()
    {
        createError = null;

        if (string.IsNullOrWhiteSpace(newNetwork))
        {
            createError = "광고 네트워크를 선택해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newPlacementId))
        {
            createError = "PlacementId를 입력해주세요.";
            return;
        }
        if (!int.TryParse(newNetwork, out var networkValue))
        {
            createError = "네트워크 값이 유효하지 않습니다.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            Network = networkValue,
            PlacementId = newPlacementId.Trim(),
            PlacementType = newPlacementType,
            RewardTableId = newRewardTableId,
            DailyLimit = newDailyLimit,
            IsEnabled = newIsEnabled,
            Description = newDescription
        };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminAdPolicies.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"광고 정책 '{newPlacementId}'이(가) 생성되었습니다.";
            await Load();
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            createError = "동일한 Network + PlacementId 조합이 이미 존재합니다.";
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>편집 모달 열기</summary>
    private void OpenEditModal(AdPolicyItem policy)
    {
        editingPolicy = policy;
        editRewardTableId = policy.RewardTableId;
        editDailyLimit = policy.DailyLimit;
        editIsEnabled = policy.IsEnabled;
        editDescription = policy.Description;
        editError = null;
        showEditModal = true;
    }

    /// <summary>편집 모달 닫기</summary>
    private void CloseEditModal() => showEditModal = false;

    /// <summary>광고 정책 수정 저장 — PUT /{id}</summary>
    private async Task SaveEdit()
    {
        if (editingPolicy is null) return;
        editError = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            RewardTableId = editRewardTableId,
            DailyLimit = editDailyLimit,
            IsEnabled = editIsEnabled,
            Description = editDescription
        };
        var response = await client.PutAsJsonAsync(ApiRoutes.AdminAdPolicies.ById(editingPolicy.Id), payload);

        if (response.IsSuccessStatusCode)
        {
            showEditModal = false;
            successMessage = "광고 정책이 수정되었습니다.";
            await Load();
        }
        else
        {
            editError = $"수정 실패: {response.StatusCode}";
        }
    }

    /// <summary>삭제 확인 모달 열기</summary>
    private void OpenDeleteModal(AdPolicyItem p)
    {
        deletingId = p.Id;
        deletingInfo = $"[{p.Network}] {p.PlacementId}";
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete() => showDeleteModal = false;

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.AdminAdPolicies.ById(deletingId));

        showDeleteModal = false;

        if (response.IsSuccessStatusCode)
        {
            successMessage = "광고 정책이 삭제되었습니다.";
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

    // 목록 응답 DTO — API 응답 역직렬화용
    private record AdPolicyItem(
        int Id,
        string Network,
        string PlacementId,
        string PlacementType,
        int? RewardTableId,
        int DailyLimit,
        bool IsEnabled,
        string Description,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    // 페이지네이션 래퍼
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
