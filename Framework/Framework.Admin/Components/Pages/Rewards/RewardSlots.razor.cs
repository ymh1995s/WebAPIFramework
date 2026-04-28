using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Rewards;

/// <summary>
/// 보상 슬롯 편집 페이지 코드-비하인드.
/// Current(이번 달) / Next(다음 달) 탭 전환 및 Day별 슬롯 저장을 담당한다.
/// </summary>
public partial class RewardSlots : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    /// <summary>현재 활성 탭 ("current" 또는 "next")</summary>
    private string activeTab = "current";

    // 로딩·에러 상태
    private bool loading = true;
    private string? errorMessage;

    // 저장 완료 메시지
    private string? saveMessage;
    private bool saveSuccess;

    /// <summary>슬롯 행 편집 모델 목록 (Day 1~28)</summary>
    private List<SlotRowModel> slotRows = new();

    // 아이템 드롭다운 목록
    private List<ItemOption> itemList = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadAllAsync, msg => { errorMessage = msg; loading = false; });
            StateHasChanged();
        }
    }

    /// <summary>아이템 목록 + 현재 탭 슬롯 데이터 병렬 로드</summary>
    private async Task LoadAllAsync()
    {
        loading = true;
        errorMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");

        var itemTask = client.GetFromJsonAsync<List<ItemOption>>(ApiRoutes.AdminItems.Collection);
        var slotTask = client.GetFromJsonAsync<List<SlotDayDto>>(ApiRoutes.AdminDailyRewardSlots.Slot(activeTab));

        await Task.WhenAll(itemTask, slotTask);

        itemList = itemTask.Result ?? new();
        slotRows = (slotTask.Result ?? new()).Select(dto => new SlotRowModel
        {
            Day = dto.Day,
            EditItemId = dto.ItemId,
            EditItemCount = dto.ItemCount
        }).ToList();

        loading = false;
    }

    /// <summary>탭 전환 — 데이터 재로드</summary>
    private async Task SwitchTab(string tab)
    {
        activeTab = tab;
        saveMessage = null;
        await SafeExecute(LoadAllAsync, msg => { errorMessage = msg; loading = false; });
        StateHasChanged();
    }

    /// <summary>특정 Day 저장</summary>
    private async Task SaveDay(SlotRowModel row)
    {
        row.Saving = true;
        saveMessage = null;
        StateHasChanged();

        try
        {
            var client = HttpClientFactory.CreateClient("ApiClient");
            // ItemId 없으면 ItemCount도 0으로 전송
            var payload = new
            {
                ItemId = row.EditItemId,
                ItemCount = row.EditItemId.HasValue ? row.EditItemCount : 0
            };
            var response = await client.PutAsJsonAsync(
                ApiRoutes.AdminDailyRewardSlots.SlotDay(activeTab, row.Day), payload);

            saveSuccess = response.IsSuccessStatusCode;
            saveMessage = saveSuccess
                ? $"Day {row.Day} 저장 완료."
                : $"Day {row.Day} 저장 실패.";

            // 저장 성공 시 EditItemCount도 동기화
            if (saveSuccess && !row.EditItemId.HasValue)
                row.EditItemCount = 0;
        }
        catch (Exception ex)
        {
            saveSuccess = false;
            saveMessage = $"저장 중 오류: {ex.Message}";
        }
        finally
        {
            row.Saving = false;
            StateHasChanged();
        }
    }

    // ─── 내부 모델 ──────────────────────────────────

    /// <summary>슬롯 행 편집 상태 모델</summary>
    private class SlotRowModel
    {
        public int Day { get; set; }
        public int? EditItemId { get; set; }
        public int EditItemCount { get; set; }
        public bool Saving { get; set; }
    }

    // API 응답 역직렬화용 DTO
    private record SlotDayDto(string Slot, int Day, int? ItemId, int ItemCount, DateTime UpdatedAt);
    private record ItemOption(int Id, string Name, string Type, bool IsDeleted);
}
