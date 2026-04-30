using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 테이블 관리 페이지 코드-비하인드.
/// 목록 조회, 생성, 설명 수정, 항목 일괄 교체, 소프트 삭제를 담당한다.
/// </summary>
public partial class RewardTables : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 필터 상태 ──────────────────────────────────
    private string filterSourceType = "";
    private string filterCode = "";

    // ─── 페이지네이션 ───────────────────────────────
    private int page = 1;
    private int pageSize = 20;

    // ─── 결과 상태 ──────────────────────────────────
    private PagedResult<RewardTableItem>? result;
    private bool isLoading;
    private string? errorMessage;
    private string? successMessage;

    // ─── 상세 편집 상태 ─────────────────────────────
    private int? expandedId;
    private RewardTableDetail? detail;
    private string editDescription = "";
    private List<EntryEditModel> editEntries = new();
    private string? detailMessage;
    private bool detailSuccess;

    // ─── 생성 모달 상태 ─────────────────────────────
    private bool showCreateModal;
    private string newSourceType = "";
    private string newCode = "";
    private string newDescription = "";
    private string? createError;

    // ─── 삭제 확인 모달 상태 ────────────────────────
    private bool showDeleteModal;
    private int deletingId;
    private string deletingInfo = "";

    // SourceType 드롭다운 옵션 — Label은 서버 enum 이름(PascalCase), Value는 정수값
    // DailyLogin(0)은 RewardTables에서 사용 중단 — UI에서 숨김 처리
    private static readonly List<(string Label, int Value)> SourceTypeOptions = new()
    {
        ("MatchComplete", 1),
        ("QuestComplete", 2),
        ("AchievementUnlock", 3),
        ("LevelUp", 4),
        ("EventReward", 5),
        ("AdminGrant", 6),
        ("AdReward", 7),
        ("Purchase", 8),
        ("StageComplete", 9),
        ("CouponCode", 10),
        ("SeasonReward", 11),
    };

    /// <summary>서버가 반환한 camelCase enum 이름의 첫 글자를 대문자로 변환 (예: "matchComplete" → "MatchComplete")</summary>
    private static string GetSourceTypeLabel(string sourceType) =>
        string.IsNullOrEmpty(sourceType) ? sourceType : char.ToUpperInvariant(sourceType[0]) + sourceType[1..];

    // SourceType별 Code 입력 예시 맵 — 생성 모달에서 동적으로 안내 문구 표시
    private static readonly Dictionary<int, string> CodeExampleMap = new()
    {
        { 1,  "match_win_ranked, match_loss_casual" },
        { 2,  "quest_001, quest_main_ch1" },
        { 3,  "ach_first_win, ach_lv50" },
        { 4,  "lv_5, lv_10, lv_default" },
        { 5,  "event_2026spring_d1" },
        { 6,  "admin_manual_001" },
        { 7,  "ad_daily_1, ad_doublegold" },
        { 8,  "iap_pkg_starter" },
        { 9,  "stage_1, stage_2, stage_boss_1" },
        { 10, "coupon_welcome, coupon_event_2026" },
        { 11, "season_2026_s1, season_ranked_gold" },
    };

    // 현재 선택된 SourceType의 Code 예시 문자열 반환 (선택 전이면 빈 문자열)
    private string CurrentCodeExample =>
        int.TryParse(newSourceType, out var v) && CodeExampleMap.TryGetValue(v, out var ex)
            ? ex
            : string.Empty;

    /// <summary>조회 실행 — 페이지 1로 리셋</summary>
    private async Task Search()
    {
        page = 1;
        await Load();
    }

    /// <summary>필터 초기화</summary>
    private void Reset()
    {
        filterSourceType = "";
        filterCode = "";
        page = 1;
        result = null;
        CloseDetail();
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

    /// <summary>실제 API 호출 — 목록 조회</summary>
    private async Task Load()
    {
        isLoading = true;
        errorMessage = null;
        successMessage = null;

        // 문자열을 int로 파싱 (빈 문자열이면 null)
        int? sourceTypeInt = int.TryParse(filterSourceType, out var st) ? st : (int?)null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var url = ApiRoutes.AdminRewardTables.Search(sourceTypeInt, filterCode, page, pageSize);
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
            result = await response.Content.ReadFromJsonAsync<PagedResult<RewardTableItem>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>상세 편집 영역 토글 — 이미 열려있으면 닫고, 다른 항목이면 API 호출 후 펼침</summary>
    private async Task OpenDetail(int id)
    {
        if (expandedId == id)
        {
            CloseDetail();
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminRewardTables.ById(id));

        if (response.IsSuccessStatusCode)
        {
            detail = await response.Content.ReadFromJsonAsync<RewardTableDetail>();
            if (detail is not null)
            {
                expandedId = id;
                editDescription = detail.Description;
                // 편집 가능한 모델로 변환
                editEntries = detail.Entries.Select(e => new EntryEditModel
                {
                    ItemId = e.ItemId,
                    Count = e.Count,
                    Weight = e.Weight
                }).ToList();
                detailMessage = null;
            }
        }
        else
        {
            errorMessage = "상세 조회 실패";
        }
    }

    /// <summary>상세 편집 영역 닫기</summary>
    private void CloseDetail()
    {
        expandedId = null;
        detail = null;
        editEntries = new();
        detailMessage = null;
    }

    /// <summary>설명(Description) 수정 저장</summary>
    private async Task SaveDescription()
    {
        if (expandedId is null) return;
        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { Description = editDescription };
        var response = await client.PutAsJsonAsync(ApiRoutes.AdminRewardTables.ById(expandedId.Value), payload);

        if (response.IsSuccessStatusCode)
        {
            detailMessage = "설명이 저장되었습니다.";
            detailSuccess = true;
            await Load(); // 목록 갱신
        }
        else
        {
            detailMessage = "설명 저장 실패";
            detailSuccess = false;
        }
    }

    /// <summary>편집 목록에 새 항목 추가</summary>
    private void AddEntry()
    {
        editEntries.Add(new EntryEditModel { ItemId = 0, Count = 1, Weight = null });
    }

    /// <summary>편집 목록에서 항목 제거</summary>
    private void RemoveEntry(EntryEditModel entry)
    {
        editEntries.Remove(entry);
    }

    /// <summary>Entries 일괄 저장 — PUT /{id}/entries</summary>
    private async Task SaveEntries()
    {
        if (expandedId is null) return;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = editEntries.Select(e => new
        {
            ItemId = e.ItemId,
            Count = e.Count,
            Weight = e.Weight
        }).ToList();

        var response = await client.PutAsJsonAsync(ApiRoutes.AdminRewardTables.Entries(expandedId.Value), payload);

        if (response.IsSuccessStatusCode)
        {
            detailMessage = "항목이 저장되었습니다.";
            detailSuccess = true;
            await Load(); // 목록 갱신 (항목 수 반영)
        }
        else
        {
            detailMessage = "항목 저장 실패";
            detailSuccess = false;
        }
    }

    /// <summary>생성 모달 열기</summary>
    private void OpenCreateModal()
    {
        newSourceType = "";
        newCode = "";
        newDescription = "";
        createError = null;
        showCreateModal = true;
    }

    /// <summary>생성 모달 닫기</summary>
    private void CloseCreateModal()
    {
        showCreateModal = false;
    }

    /// <summary>보상 테이블 생성 — POST /</summary>
    private async Task Create()
    {
        createError = null;

        if (string.IsNullOrWhiteSpace(newSourceType))
        {
            createError = "SourceType을 선택해주세요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(newCode))
        {
            createError = "Code를 입력해주세요.";
            return;
        }

        if (!int.TryParse(newSourceType, out var sourceTypeValue))
        {
            createError = "SourceType 값이 유효하지 않습니다.";
            return;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new
        {
            SourceType = sourceTypeValue,
            Code = newCode,
            Description = newDescription
        };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminRewardTables.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            showCreateModal = false;
            successMessage = $"보상 테이블 '{newCode}'이(가) 생성되었습니다.";
            await Load();
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            createError = "동일한 SourceType + Code 조합이 이미 존재합니다.";
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Code 형식 또는 길이 검증 실패 — 서버 메시지 표시
            var errorBody = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            createError = errorBody?.Message ?? "Code 형식이 올바르지 않습니다.";
        }
        else
        {
            createError = $"생성 실패: {response.StatusCode}";
        }
    }

    /// <summary>삭제 확인 모달 열기</summary>
    private void OpenDeleteModal(RewardTableItem t)
    {
        deletingId = t.Id;
        deletingInfo = $"[{t.SourceType}] {t.Code}";
        showDeleteModal = true;
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete()
    {
        showDeleteModal = false;
        deletingId = 0;
        deletingInfo = "";
    }

    /// <summary>소프트 삭제 확정 — DELETE /{id}</summary>
    private async Task ConfirmDelete()
    {
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.AdminRewardTables.ById(deletingId));

        showDeleteModal = false;

        if (response.IsSuccessStatusCode)
        {
            successMessage = $"보상 테이블이 삭제되었습니다.";
            // 현재 펼쳐진 상세가 삭제 대상이면 닫기
            if (expandedId == deletingId) CloseDetail();
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

    // 항목 편집용 가변 모델
    private class EntryEditModel
    {
        public int ItemId { get; set; }
        public int Count { get; set; } = 1;
        public int? Weight { get; set; }
    }

    // 목록 응답 DTO
    private record RewardTableItem(int Id, string SourceType, string Code, string Description, bool IsDeleted, int EntryCount);

    // 상세 응답 DTO
    private record RewardTableDetail(int Id, string SourceType, string Code, string Description, bool IsDeleted, List<EntryDto> Entries);
    private record EntryDto(int Id, int ItemId, string ItemName, int Count, int? Weight);

    // 페이지네이션 래퍼
    private record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // API 에러 응답 DTO — BadRequest 메시지 역직렬화용
    private record ErrorResponse(string Message);
}
