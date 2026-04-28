using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Items;

/// <summary>
/// 아이템 마스터 관리 페이지 코드-비하인드.
/// 아이템 추가, 수정, 삭제 기능을 담당한다.
/// </summary>
public partial class Items : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // 아이템 목록
    private List<ItemDto>? items;
    private bool isLoading = true;
    private string? listMessage;

    // 추가 폼 상태
    private string newName = "";
    private ItemType newItemType = ItemType.Currency;
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
        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminItems.Collection);
        if (response.IsSuccessStatusCode)
            items = await response.Content.ReadFromJsonAsync<List<ItemDto>>();
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

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { Name = newName, ItemType = newItemType, Description = newDescription, AuditLevel = newAuditLevel, AnomalyThreshold = newAnomalyThreshold };
        var response = await client.PostAsJsonAsync(ApiRoutes.AdminItems.Collection, payload);

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
        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { Name = editName, ItemType = editItemType, Description = editDescription, AuditLevel = editAuditLevel, AnomalyThreshold = editAnomalyThreshold };
        var response = await client.PutAsJsonAsync(ApiRoutes.AdminItems.ById(id), payload);

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
        var client = HttpClientFactory.CreateClient("ApiClient");
        var result = await client.GetFromJsonAsync<HolderCountDto>(ApiRoutes.AdminItems.Holders(id));
        holderCount = result?.Count ?? 0;
        confirmDeleteId = id;
    }

    /// <summary>삭제 확인</summary>
    private async Task ConfirmDelete()
    {
        if (confirmDeleteId == null) return;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.AdminItems.ById(confirmDeleteId!.Value));

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

    // API 응답 매핑용 로컬 DTO
    private record ItemDto(int Id, string Name, ItemType ItemType, string Description, AuditLevel AuditLevel, int AnomalyThreshold);
    private record HolderCountDto(int Count);
    private enum ItemType { Currency, Consumable }
    /// <summary>감사 로그 기록 수준 — 서버 Framework.Domain.Enums.AuditLevel와 순서 동일해야 함</summary>
    private enum AuditLevel { AnomalyOnly, Full }
}
