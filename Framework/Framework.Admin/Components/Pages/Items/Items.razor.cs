using Framework.Admin.Components;
using Framework.Admin.Constants;
using Framework.Admin.Http;
using Framework.Admin.Json;
using Framework.Application.Features.Item;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Items;

/// <summary>
/// 아이템 마스터 관리 페이지 코드-비하인드.
/// 아이템 추가, 수정, 삭제 기능을 담당한다.
/// </summary>
public partial class Items : SafeComponentBase
{
    // 의존성 주입 — ApiHttpClient 래퍼를 통해 camelCase enum JSON 옵션 일관 적용
    [Inject] private ApiHttpClient ApiClient { get; set; } = default!;

    // 아이템 목록
    private List<ItemDto>? items;
    private bool isLoading = true;
    private string? listMessage;

    // 추가 폼 상태
    private string newName = "";
    // 신규 아이템 기본 타입 — 통화 아이템(Gold/Gems)은 예약 ItemId이므로 일반 Consumable을 기본으로 설정
    private ItemType newItemType = ItemType.Consumable;
    private string newDescription = "";
    private AuditLevel newAuditLevel = AuditLevel.AnomalyOnly;
    private int newAnomalyThreshold = 0;
    private string? createMessage;
    private bool createSuccess;

    // 수정 상태
    private int? editingId;
    private string editName = "";
    private ItemType editItemType;
    private string editDescription = "";
    private AuditLevel editAuditLevel;
    private int editAnomalyThreshold;

    // 삭제 확인 팝업 상태
    private int? confirmDeleteId;
    private int holderCount;

    // 사용 효과 편집 상태 — 아이템별 인라인 편집 여부 및 입력값
    // 키: ItemId, 값: 편집 중인 RewardTableId 문자열 (null 표현을 위해 string 사용)
    private Dictionary<int, string> useEffectEditMap = new();
    // 사용 효과 저장 피드백 메시지 (키: ItemId)
    private Dictionary<int, (string Message, bool IsSuccess)> useEffectMessages = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeExecute(LoadItems, msg => listMessage = msg);
            StateHasChanged();
        }
    }

    /// <summary>목록 조회</summary>
    private async Task LoadItems()
    {
        isLoading = true;
        listMessage = null;

        // GetRawAsync로 응답 코드 확인 후 AdminJsonOptions.Default로 역직렬화
        var response = await ApiClient.GetRawAsync(ApiRoutes.AdminItems.Collection);
        if (response.IsSuccessStatusCode)
            items = await response.Content.ReadFromJsonAsync<List<ItemDto>>(AdminJsonOptions.Default);
        else
            listMessage = $"목록 조회 실패: {response.StatusCode}";
        isLoading = false;
    }

    /// <summary>아이템 생성</summary>
    private async Task CreateItem()
    {
        createMessage = null;
        if (string.IsNullOrWhiteSpace(newName))
        {
            createMessage = "이름을 입력해주세요.";
            createSuccess = false;
            return;
        }

        var payload = new { Name = newName, ItemType = newItemType, Description = newDescription, AuditLevel = newAuditLevel, AnomalyThreshold = newAnomalyThreshold };
        // PostAsync — AdminJsonOptions.Default로 enum 문자열 직렬화
        var response = await ApiClient.PostAsync(ApiRoutes.AdminItems.Collection, payload);

        if (response.IsSuccessStatusCode)
        {
            createMessage = "추가되었습니다.";
            createSuccess = true;
            newName = "";
            newDescription = "";
            newAnomalyThreshold = 0;
            newAuditLevel = AuditLevel.AnomalyOnly;
            await LoadItems();
        }
        else
        {
            createMessage = "추가에 실패했습니다.";
            createSuccess = false;
        }
    }

    /// <summary>수정 시작 — 현재 값을 편집 필드에 채움</summary>
    private void StartEdit(ItemDto item)
    {
        editingId = item.Id;
        editName = item.Name;
        editItemType = item.ItemType;
        editDescription = item.Description;
        editAuditLevel = item.AuditLevel;
        editAnomalyThreshold = item.AnomalyThreshold;
    }

    /// <summary>수정 취소</summary>
    private void CancelEdit()
    {
        editingId = null;
    }

    /// <summary>수정 저장</summary>
    private async Task SaveEdit(int id)
    {
        var payload = new { Name = editName, ItemType = editItemType, Description = editDescription, AuditLevel = editAuditLevel, AnomalyThreshold = editAnomalyThreshold };
        // PutAsync — AdminJsonOptions.Default로 enum 문자열 직렬화
        var response = await ApiClient.PutAsync(ApiRoutes.AdminItems.ById(id), payload);

        if (response.IsSuccessStatusCode)
        {
            editingId = null;
            await LoadItems();
        }
        else
        {
            listMessage = "수정에 실패했습니다.";
        }
    }

    /// <summary>삭제 요청 — 보유 유저 수 조회 후 팝업 표시</summary>
    private async Task RequestDelete(int id)
    {
        listMessage = null;
        // GetAsync<T>로 역직렬화까지 한 번에 처리
        var result = await ApiClient.GetAsync<HolderCountDto>(ApiRoutes.AdminItems.Holders(id));
        holderCount = result?.Count ?? 0;
        confirmDeleteId = id;
    }

    /// <summary>삭제 확인</summary>
    private async Task ConfirmDelete()
    {
        if (confirmDeleteId == null) return;

        var response = await ApiClient.DeleteAsync(ApiRoutes.AdminItems.ById(confirmDeleteId!.Value));

        confirmDeleteId = null;

        if (response.IsSuccessStatusCode)
            await LoadItems();
        else
            listMessage = "삭제에 실패했습니다.";
    }

    /// <summary>삭제 취소</summary>
    private void CancelDelete()
    {
        confirmDeleteId = null;
    }

    /// <summary>사용 효과 편집 시작 — 현재 RewardTableId를 입력 필드에 채움</summary>
    private void StartEditUseEffect(ItemDto item)
    {
        // 기존 편집 중이면 값 유지, 새로 시작이면 현재 값으로 초기화
        useEffectEditMap[item.Id] = item.UseRewardTableId?.ToString() ?? "";
    }

    /// <summary>사용 효과 편집 취소</summary>
    private void CancelEditUseEffect(int itemId)
    {
        useEffectEditMap.Remove(itemId);
        useEffectMessages.Remove(itemId);
    }

    /// <summary>사용 효과 저장 — 입력값을 int?로 파싱하여 PUT 요청</summary>
    private async Task SaveUseEffect(int itemId)
    {
        // 입력값 파싱 — 빈 문자열은 null(보상 없음)으로 처리
        int? rewardTableId = null;
        if (useEffectEditMap.TryGetValue(itemId, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            if (!int.TryParse(raw, out var parsed) || parsed <= 0)
            {
                useEffectMessages[itemId] = ("올바른 RewardTable ID를 입력하세요.", false);
                return;
            }
            rewardTableId = parsed;
        }

        var payload = new { RewardTableId = rewardTableId };
        var response = await ApiClient.PutAsync(ApiRoutes.AdminItems.UseEffect(itemId), payload);

        if (response.IsSuccessStatusCode)
        {
            useEffectMessages[itemId] = ("저장되었습니다.", true);
            useEffectEditMap.Remove(itemId);
            // 목록 갱신하여 UseRewardTableId 반영
            await LoadItems();
        }
        else
        {
            useEffectMessages[itemId] = ("저장에 실패했습니다.", false);
        }
    }

    // 보유 유저 수 조회 응답용 로컬 DTO — 서버 대응 타입 없음
    private record HolderCountDto(int Count);
}
