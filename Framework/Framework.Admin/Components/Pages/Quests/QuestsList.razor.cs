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

    // ─── 드롭다운 데이터 소스 ────────────────────────
    // 조건 대상 ID 드롭다운 — 조건 타입별로 선택적 사용
    private List<DropdownStageOption> allStages = new();
    private List<DropdownItemOption> allItems = new();
    private List<DropdownShopProductOption> allShopProducts = new();

    // 보상 테이블 드롭다운 — SourceType=QuestComplete(2)만 표시
    private List<DropdownRewardTableOption> allRewardTables = new();

    // 선행 퀘스트 드롭다운 — 활성 퀘스트 전체 캐시 (검색 결과와 별개)
    private List<QuestItem> allActiveQuests = new();

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

    /// <summary>
    /// 페이지 초기화 — 드롭다운에 필요한 데이터 소스를 병렬로 사전 로드한다.
    /// 로드 실패 시 해당 드롭다운은 비어 있는 상태로 동작하고 페이지는 정상 표시된다.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        // 5개 데이터 소스를 병렬 요청 — 순서 독립적이므로 WhenAll 사용
        var stagesTask      = ApiClient.GetRawAsync(ApiRoutes.AdminStages.Search(null, 1, 100));
        var itemsTask       = ApiClient.GetRawAsync(ApiRoutes.AdminItems.Collection);
        var shopTask        = ApiClient.GetRawAsync(ApiRoutes.AdminShopProducts.Search(null, 1, 100));
        var rewardTask      = ApiClient.GetRawAsync(ApiRoutes.AdminRewardTables.Search(2, null, 1, 100));
        var questsTask      = ApiClient.GetRawAsync(ApiRoutes.AdminQuests.Search(null, null, true, 1, 100));

        await Task.WhenAll(stagesTask, itemsTask, shopTask, rewardTask, questsTask);

        // 스테이지 목록 역직렬화
        if (stagesTask.Result.IsSuccessStatusCode)
        {
            var paged = await stagesTask.Result.Content
                .ReadFromJsonAsync<PagedResultDto<DropdownStageOption>>(AdminJsonOptions.Default);
            allStages = paged?.Items ?? new();
        }

        // 아이템 목록 역직렬화
        if (itemsTask.Result.IsSuccessStatusCode)
        {
            allItems = await itemsTask.Result.Content
                .ReadFromJsonAsync<List<DropdownItemOption>>(AdminJsonOptions.Default) ?? new();
        }

        // 상점 상품 목록 역직렬화
        if (shopTask.Result.IsSuccessStatusCode)
        {
            var paged = await shopTask.Result.Content
                .ReadFromJsonAsync<PagedResultDto<DropdownShopProductOption>>(AdminJsonOptions.Default);
            allShopProducts = paged?.Items ?? new();
        }

        // 보상 테이블 목록 역직렬화 — SourceType=QuestComplete(2)만
        if (rewardTask.Result.IsSuccessStatusCode)
        {
            var paged = await rewardTask.Result.Content
                .ReadFromJsonAsync<PagedResultDto<DropdownRewardTableOption>>(AdminJsonOptions.Default);
            allRewardTables = paged?.Items ?? new();
        }

        // 활성 퀘스트 전체 목록 역직렬화 — 선행 퀘스트 드롭다운용
        if (questsTask.Result.IsSuccessStatusCode)
        {
            var paged = await questsTask.Result.Content
                .ReadFromJsonAsync<PagedResultDto<QuestItem>>(AdminJsonOptions.Default);
            allActiveQuests = paged?.Items ?? new();
        }
    }

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

    /// <summary>
    /// 생성 모달 조건 타입 변경 핸들러 — 타입 변경 시 조건 대상 ID를 null로 리셋한다.
    /// @bind:after로 바인딩 이후 호출되도록 razor에서 연결.
    /// </summary>
    private void OnNewConditionTypeChanged()
    {
        // 조건 타입이 바뀌면 이전 선택값이 무의미해지므로 null 초기화
        newConditionTargetId = null;
    }

    /// <summary>
    /// 편집 모달 조건 타입 변경 핸들러 — 타입 변경 시 조건 대상 ID를 null로 리셋한다.
    /// </summary>
    private void OnEditConditionTypeChanged()
    {
        editConditionTargetId = null;
    }

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
            createError = "보상 테이블을 선택해주세요.";
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

    /// <summary>
    /// 보상 테이블 드롭다운 표시 레이블 — Description 유무에 따라 다르게 표시.
    /// Description이 있으면 "[Code] Description", 없으면 "[Code]"만 표시.
    /// </summary>
    private static string RewardTableLabel(DropdownRewardTableOption rt) =>
        string.IsNullOrWhiteSpace(rt.Description)
            ? $"[{rt.Code}]"
            : $"[{rt.Code}] {rt.Description}";

    /// <summary>선행 퀘스트 드롭다운 표시 레이블 — "[Code] Title (주기)" 형식</summary>
    private string QuestDropdownLabel(QuestItem q) =>
        $"[{q.Code}] {q.Title} ({PeriodLabel(q.Period)})";

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

    // 스테이지 드롭다운 최소 DTO — "[Code] Name" 표시용
    private record DropdownStageOption(int Id, string Code, string Name);

    // 아이템 드롭다운 최소 DTO — "Name (ItemType)" 표시용
    private record DropdownItemOption(int Id, string Name, string ItemType, bool IsDeleted);

    // 상점 상품 드롭다운 최소 DTO — "Name" 표시용
    private record DropdownShopProductOption(int Id, string Name, bool IsEnabled);

    // 보상 테이블 드롭다운 최소 DTO — "[Code] Description" 표시용
    private record DropdownRewardTableOption(int Id, string Code, string Description, bool IsDeleted);
}
