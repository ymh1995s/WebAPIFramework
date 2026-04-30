// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Content;

/// <summary>
/// 스테이지 관리 페이지 코드-비하인드.
/// 스테이지 마스터 데이터의 목록 조회, 생성, 수정을 담당한다.
/// </summary>
public partial class Stages : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterKeyword = "";

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResult<StageItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newCode = "";
    private string newName = "";
    private string newRewardTableCode = "";
    private string newRePlayRewardTableCode = "";
    private int newDecayPercent = 0;
    private int newExpReward = 0;
    private int? newRequiredPrevStageId;
    private bool newIsActive = true;
    private int newSortOrder = 0;
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private StageItem? editingStage;
    private string editName = "";
    private string editRewardTableCode = "";
    private string editRePlayRewardTableCode = "";
    private int editDecayPercent = 0;
    private int editExpReward = 0;
    private int? editRequiredPrevStageId;
    private bool editIsActive;
    private int editSortOrder = 0;
    private string? editError;

    /// <summary>조회 실행 — 페이지 1로 리셋</summary>
    private async Task Search()
    {
        page = 1;
        await Load();
    }

    /// <summary>필터 초기화</summary>
    private void Reset()
    {
        filterKeyword = "";
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

    /// <summary>스테이지 목록 API 호출</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminStages.Search(filterKeyword, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<StageItem>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newCode = "";
        newName = "";
        newRewardTableCode = "";
        newRePlayRewardTableCode = "";
        newDecayPercent = 0;
        newExpReward = 0;
        newRequiredPrevStageId = null;
        newIsActive = true;
        newSortOrder = 0;
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal() => showCreateModal = false;

    /// <summary>스테이지 생성 — POST /api/admin/stages</summary>
    private async Task Create()
    {
        createError = null;

        if (string.IsNullOrWhiteSpace(newCode))
        {
            createError = "스테이지 코드를 입력해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newName))
        {
            createError = "스테이지 이름을 입력해주세요.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            Code = newCode.Trim(),
            Name = newName.Trim(),
            RewardTableCode = string.IsNullOrWhiteSpace(newRewardTableCode) ? null : newRewardTableCode.Trim(),
            RePlayRewardTableCode = string.IsNullOrWhiteSpace(newRePlayRewardTableCode) ? null : newRePlayRewardTableCode.Trim(),
            RePlayRewardDecayPercent = newDecayPercent,
            ExpReward = newExpReward,
            RequiredPrevStageId = newRequiredPrevStageId,
            IsActive = newIsActive,
            SortOrder = newSortOrder
        };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminStages.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"스테이지 '{newName}'이(가) 생성되었습니다.";
            await Load();
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            createError = "동일한 코드의 스테이지가 이미 존재합니다.";
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>편집 모달 열기</summary>
    private void OpenEditModal(StageItem s)
    {
        editingStage = s;
        editName = s.Name;
        editRewardTableCode = s.RewardTableCode ?? "";
        editRePlayRewardTableCode = s.RePlayRewardTableCode ?? "";
        editDecayPercent = s.RePlayRewardDecayPercent;
        editExpReward = s.ExpReward;
        editRequiredPrevStageId = s.RequiredPrevStageId;
        editIsActive = s.IsActive;
        editSortOrder = s.SortOrder;
        editError = null;
        showEditModal = true;
    }

    /// <summary>편집 모달 닫기</summary>
    private void CloseEditModal() => showEditModal = false;

    /// <summary>스테이지 수정 저장 — PUT /api/admin/stages/{id}</summary>
    private async Task SaveEdit()
    {
        if (editingStage is null) return;
        editError = null;

        if (string.IsNullOrWhiteSpace(editName))
        {
            editError = "이름을 입력해주세요.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            Name = editName.Trim(),
            RewardTableCode = string.IsNullOrWhiteSpace(editRewardTableCode) ? null : editRewardTableCode.Trim(),
            RePlayRewardTableCode = string.IsNullOrWhiteSpace(editRePlayRewardTableCode) ? null : editRePlayRewardTableCode.Trim(),
            RePlayRewardDecayPercent = editDecayPercent,
            ExpReward = editExpReward,
            RequiredPrevStageId = editRequiredPrevStageId,
            IsActive = editIsActive,
            SortOrder = editSortOrder
        };
        var response = await client.PutAsJsonAsync(ApiRoutes.AdminStages.ById(editingStage.Id), payload);

        if (response.IsSuccessStatusCode)
        {
            showEditModal = false;
            successMessage = "스테이지가 수정되었습니다.";
            await Load();
        }
        else
        {
            editError = $"수정 실패: {response.StatusCode}";
        }
    }

    // ─── 내부 모델 ──────────────────────────────────

    // 목록 응답 DTO — API 응답 역직렬화용
    private record StageItem(
        int Id,
        string Code,
        string Name,
        string? RewardTableCode,
        string? RePlayRewardTableCode,
        int RePlayRewardDecayPercent,
        int ExpReward,
        int? RequiredPrevStageId,
        bool IsActive,
        int SortOrder
    );

    // 페이지네이션 래퍼
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
