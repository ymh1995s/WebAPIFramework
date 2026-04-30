using Framework.Admin.Components.Base;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 슬롯 편집 페이지 코드-비하인드.
/// Current(이번 달) / Next(다음 달) 탭 전환 및 전체 Day 일괄 저장을 담당.
/// DirtyGuardBase를 상속하여 저장하지 않은 변경사항 이탈 경고를 자동 처리.
/// </summary>
public partial class RewardSlots : DirtyGuardBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    /// <summary>현재 활성 탭 ("current" 또는 "next")</summary>
    private string activeTab = "current";

    // 로딩·에러 상태
    private bool loading = true;
    private string? errorMessage;

    // 전체 저장 상태 및 결과 메시지
    private bool saving = false;
    private string? saveMessage;
    private bool saveSuccess;

    /// <summary>슬롯 행 편집 모델 목록 (Day 1~28)</summary>
    private List<SlotRowModel> slotRows = new();

    // 아이템 드롭다운 목록
    private List<ItemOption> itemList = new();

    /// <summary>
    /// 현재 편집 내용이 원본 스냅샷과 다른지 여부.
    /// 전체 저장 버튼 활성화 조건으로 사용.
    /// </summary>
    private bool HasChanges => slotRows.Any(r =>
        r.EditItemId != r.OriginalItemId || r.EditItemCount != r.OriginalItemCount);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // base 먼저 호출하여 DirtyGuardBase의 JS 초기화 실행
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await SafeExecute(LoadAllAsync, msg => { errorMessage = msg; loading = false; });
            StateHasChanged();
        }
    }

    /// <summary>아이템 목록 + 현재 탭 슬롯 데이터 병렬 로드 후 원본 스냅샷 저장</summary>
    private async Task LoadAllAsync()
    {
        loading = true;
        errorMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");

        // 아이템 목록과 슬롯 데이터 병렬 요청
        var itemTask = client.GetFromJsonAsync<List<ItemOption>>(ApiRoutes.AdminItems.Collection);
        var slotTask = client.GetFromJsonAsync<List<SlotDayDto>>(ApiRoutes.AdminDailyRewardSlots.Slot(activeTab));

        await Task.WhenAll(itemTask, slotTask);

        itemList = itemTask.Result ?? new();

        // 로드 완료 후 원본 스냅샷도 함께 저장하여 변경 감지에 활용
        slotRows = (slotTask.Result ?? new()).Select(dto => new SlotRowModel
        {
            Day = dto.Day,
            EditItemId = dto.ItemId,
            EditItemCount = dto.ItemCount,
            OriginalItemId = dto.ItemId,
            OriginalItemCount = dto.ItemCount
        }).ToList();

        loading = false;

        // 새 데이터 로드 후 깨끗한 상태로 초기화
        await MarkCleanAsync();
    }

    /// <summary>탭 전환 — IsDirty 상태면 confirm 후 이동, 아니면 즉시 전환</summary>
    private async Task SwitchTab(string tab)
    {
        if (activeTab == tab) return;

        // 변경사항이 있으면 이탈 여부 확인
        if (IsDirty)
        {
            var confirmed = await JS.InvokeAsync<bool>(
                "confirm", new object[] { "저장하지 않은 변경사항이 있습니다. 탭을 전환하시겠습니까?" });
            if (!confirmed) return;
        }

        activeTab = tab;
        saveMessage = null;
        await SafeExecute(LoadAllAsync, msg => { errorMessage = msg; loading = false; });
        StateHasChanged();
    }

    /// <summary>드롭다운 변경 핸들러 — 아이템 선택 변경 시 호출</summary>
    private async Task OnItemChanged(SlotRowModel row, ChangeEventArgs e)
    {
        // 선택값 파싱 후 EditItemId 업데이트
        row.EditItemId = string.IsNullOrEmpty(e.Value?.ToString())
            ? null
            : int.Parse(e.Value.ToString()!);

        // ItemId가 없어진 경우 수량도 0으로 초기화
        if (!row.EditItemId.HasValue)
            row.EditItemCount = 0;

        await MarkDirtyAsync();
        StateHasChanged();
    }

    /// <summary>수량 변경 핸들러 — 입력 후 dirty 상태로 표시</summary>
    private async Task OnItemCountChanged(SlotRowModel row, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var count))
            row.EditItemCount = count;

        await MarkDirtyAsync();
        StateHasChanged();
    }

    /// <summary>전체 Day 일괄 저장 — 변경된 행만 수집하여 PUT 전송 (all-or-nothing)</summary>
    private async Task SaveAll()
    {
        // 중복 저장 방지
        if (saving) return;

        saving = true;
        saveMessage = null;
        StateHasChanged();

        try
        {
            // 변경된 행만 필터링하여 페이로드 구성
            var changedDays = slotRows
                .Where(r => r.EditItemId != r.OriginalItemId || r.EditItemCount != r.OriginalItemCount)
                .Select(r => new
                {
                    Day = r.Day,
                    ItemId = r.EditItemId,
                    ItemCount = r.EditItemId.HasValue ? r.EditItemCount : 0
                })
                .ToList();

            // 변경 항목이 없으면 저장 불필요 (HasChanges 체크로 보통 진입하지 않음)
            if (changedDays.Count == 0)
            {
                saveMessage = "변경된 내용이 없습니다.";
                saveSuccess = false;
                return;
            }

            var client = HttpClientFactory.CreateClient("ApiClient");
            var payload = new { Days = changedDays };
            var response = await client.PutAsJsonAsync(
                ApiRoutes.AdminDailyRewardSlots.SlotBatch(activeTab), payload);

            saveSuccess = response.IsSuccessStatusCode;
            if (saveSuccess)
            {
                // 성공 시 원본 스냅샷 갱신 — 이후 변경 감지 기준점이 됨
                foreach (var row in slotRows)
                {
                    row.OriginalItemId = row.EditItemId;
                    row.OriginalItemCount = row.EditItemCount;
                }
                saveMessage = "전체 저장 완료.";
                await MarkCleanAsync();
            }
            else
            {
                // 실패 시 응답 내용 표시
                var errorBody = await response.Content.ReadAsStringAsync();
                saveMessage = $"저장 실패: {(string.IsNullOrEmpty(errorBody) ? response.StatusCode.ToString() : errorBody)}";
            }
        }
        catch (Exception ex)
        {
            saveSuccess = false;
            saveMessage = $"저장 중 오류: {ex.Message}";
        }
        finally
        {
            saving = false;
            StateHasChanged();
        }
    }

    // ─── 내부 모델 ──────────────────────────────────

    /// <summary>슬롯 행 편집 상태 모델 — 원본 스냅샷과 편집값을 함께 보유</summary>
    private class SlotRowModel
    {
        public int Day { get; set; }

        // 현재 편집 중인 값
        public int? EditItemId { get; set; }
        public int EditItemCount { get; set; }

        // 마지막 로드/저장 시점의 원본값 (변경 감지 기준)
        public int? OriginalItemId { get; set; }
        public int OriginalItemCount { get; set; }

        /// <summary>현재 행이 원본과 다른지 여부 (시각적 표시용)</summary>
        public bool IsChanged =>
            EditItemId != OriginalItemId || EditItemCount != OriginalItemCount;
    }

    // API 응답 역직렬화용 DTO
    private record SlotDayDto(string Slot, int Day, int? ItemId, int ItemCount, DateTime UpdatedAt);
    private record ItemOption(int Id, string Name, string Type, bool IsDeleted);
}
