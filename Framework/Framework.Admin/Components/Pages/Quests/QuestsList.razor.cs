using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Common;
using Framework.Application.Features.Quest;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Quests;

/// <summary>
/// 퀘스트 관리 페이지 코드-비하인드.
/// 퀘스트 정의 목록 조회, 생성, 수정, 소프트 삭제를 담당한다.
/// </summary>
public partial class QuestsList : SafeComponentBase
{
    // ApiHttpClient — camelCase JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterKeyword = "";
    private string filterPeriod = "";   // "" = 전체, "0"=일일, "1"=주간, "2"=영구
    private string filterIsActive = ""; // "" = 전체, "true"/"false"

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResultDto<QuestItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newCode = "";
    private string newTitle = "";
    private string newDescription = "";
    private int newPeriod = 0;          // 기본값: 일일
    private int newConditionType = 1;   // 기본값: 스테이지 클리어
    private int? newConditionTargetId = null;
    private int newTargetAmount = 1;
    private int newRewardTableId;
    private int? newPrerequisiteQuestId = null;
    private int newSortOrder = 0;
    private bool newIsActive = true;
    private string? createError;

    // ─── 편집 모달 상태 ─────────────────────────────
    private bool showEditModal;
    private QuestItem? editingQuest;
    private string editTitle = "";
    private string editDescription = "";
    private int editConditionType;
    private int? editConditionTargetId;
    private int editTargetAmount;
    private int editRewardTableId;
    private int? editPrerequisiteQuestId;
    private int editSortOrder;
    private bool editIsActive;
    private string? editError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingCode = "";

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
        filterPeriod = "";
        filterIsActive = "";
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

    /// <summary>퀘스트 목록 조회 — GET /api/admin/quests</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        // 필터 파라미터 파싱
        int? periodInt = int.TryParse(filterPeriod, out var p) ? p : (int?)null;
        bool? isActiveBool = bool.TryParse(filterIsActive, out var a) ? a : (bool?)null;

        var url = ApiRoutes.AdminQuests.Search(
            string.IsNullOrWhiteSpace(filterKeyword) ? null : filterKeyword,
            periodInt, isActiveBool, page, pageSize);

        var response = await ApiClient.GetRawAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResultDto<QuestItem>>(AdminJsonOptions.Default);
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newCode = "";
        newTitle = "";
        newDescription = "";
        newPeriod = 0;
        newConditionType = 1;
        newConditionTargetId = null;
        newTargetAmount = 1;
        newRewardTableId = 0;
        newPrerequisiteQuestId = null;
        newSortOrder = 0;
        newIsActive = true;
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal() => showCreateModal = false;

    /// <summary>퀘스트 생성 — POST /api/admin/quests</summary>
    private async Task Create()
    {
        createError = null;

        // 필수 필드 검증
        if (string.IsNullOrWhiteSpace(newCode))
        {
            createError = "코드를 입력해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            createError = "제목을 입력해주세요.";
            return;
        }
        if (newTargetAmount < 1)
        {
            createError = "목표 수량은 1 이상이어야 합니다.";
            return;
        }
        if (newRewardTableId < 1)
        {
            createError = "RewardTableId를 1 이상으로 입력해주세요.";
            return;
        }

        var payload = new
        {
            Code = newCode.Trim(),
            Title = newTitle.Trim(),
            Description = string.IsNullOrWhiteSpace(newDescription) ? (string?)null : newDescription.Trim(),
            Period = newPeriod,
            ConditionType = newConditionType,
            ConditionTargetId = newConditionTargetId,
            TargetAmount = newTargetAmount,
            RewardTableId = newRewardTableId,
            PrerequisiteQuestId = newPrerequisiteQuestId,
            IsActive = newIsActive,
            SortOrder = newSortOrder,
        };

        var response = await ApiClient.PostAsync(ApiRoutes.AdminQuests.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"퀘스트 '{newTitle}'이(가) 생성되었습니다.";
            await Load();
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>편집 모달 열기</summary>
    private void OpenEditModal(QuestItem quest)
    {
        editingQuest = quest;
        editTitle = quest.Title;
        editDescription = quest.Description ?? "";
        editConditionType = (int)quest.ConditionType;
        editConditionTargetId = quest.ConditionTargetId;
        editTargetAmount = quest.TargetAmount;
        editRewardTableId = quest.RewardTableId;
        editPrerequisiteQuestId = quest.PrerequisiteQuestId;
        editSortOrder = quest.SortOrder;
        editIsActive = quest.IsActive;
        editError = null;
        showEditModal = true;
    }

    /// <summary>편집 모달 닫기</summary>
    private void CloseEditModal() => showEditModal = false;

    /// <summary>퀘스트 수정 저장 — PUT /{id}</summary>
    private async Task SaveEdit()
    {
        if (editingQuest is null) return;
        editError = null;

        var payload = new
        {
            Title = editTitle.Trim(),
            Description = string.IsNullOrWhiteSpace(editDescription) ? (string?)null : editDescription.Trim(),
            ConditionType = editConditionType,
            ConditionTargetId = editConditionTargetId,
            TargetAmount = editTargetAmount,
            RewardTableId = editRewardTableId,
            PrerequisiteQuestId = editPrerequisiteQuestId,
            IsActive = editIsActive,
            SortOrder = editSortOrder,
        };

        var response = await ApiClient.PutAsync(ApiRoutes.AdminQuests.ById(editingQuest.Id), payload);

        if (response.IsSuccessStatusCode)
        {
            showEditModal = false;
            successMessage = "퀘스트 정보가 수정되었습니다.";
            await Load();
        }
        else
        {
            editError = $"수정 실패: {response.StatusCode}";
        }
    }

    /// <summary>삭제 확인 모달 열기</summary>
    private void OpenDeleteModal(QuestItem q)
    {
        deletingId = q.Id;
        deletingCode = q.Code;
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete() => showDeleteModal = false;

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var response = await ApiClient.DeleteAsync(ApiRoutes.AdminQuests.ById(deletingId));

        showDeleteModal = false;

        if (response.IsSuccessStatusCode)
        {
            successMessage = "퀘스트가 삭제되었습니다.";
            await Load();
        }
        else
        {
            errorMessage = "삭제 실패";
        }

        deletingId = 0;
        deletingCode = "";
    }

    // 주기 레이블 반환
    private static string PeriodLabel(QuestPeriod period) => period switch
    {
        QuestPeriod.Daily => "일일",
        QuestPeriod.Weekly => "주간",
        QuestPeriod.Permanent => "영구",
        _ => period.ToString()
    };

    // 조건 타입 레이블 반환
    private static string ConditionLabel(QuestConditionType type) => type switch
    {
        QuestConditionType.StageCleared => "스테이지 클리어",
        QuestConditionType.ItemUsed => "아이템 사용",
        QuestConditionType.ShopPurchased => "상점 구매",
        QuestConditionType.Login => "로그인",
        _ => type.ToString()
    };

    // ─── 내부 모델 ──────────────────────────────────

    // 퀘스트 정의 목록 응답 DTO — AdminJsonOptions.Default로 역직렬화
    private record QuestItem(
        int Id,
        string Code,
        string Title,
        string? Description,
        QuestPeriod Period,
        QuestConditionType ConditionType,
        int? ConditionTargetId,
        int TargetAmount,
        int RewardTableId,
        int? PrerequisiteQuestId,
        bool IsActive,
        int SortOrder,
        bool IsDeleted,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );
}
