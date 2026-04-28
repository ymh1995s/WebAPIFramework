using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Players;

/// <summary>
/// 플레이어 인벤토리 조회 페이지 코드-비하인드.
/// 플레이어 ID를 입력받아 보유 아이템 목록을 조회한다.
/// </summary>
public partial class Inventory : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // 조회 대상 플레이어 ID
    private int playerId;
    private bool isLoading;
    private List<PlayerItemDto>? items;
    private string? errorMessage;

    /// <summary>인벤토리 조회 — 플레이어 ID 유효성 검사 후 API 호출</summary>
    private async Task LoadInventory()
    {
        if (playerId <= 0)
        {
            errorMessage = "유효한 플레이어 ID를 입력하세요.";
            return;
        }

        isLoading = true;
        errorMessage = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.Players.Items(playerId));

        if (response.IsSuccessStatusCode)
            items = await response.Content.ReadFromJsonAsync<List<PlayerItemDto>>();
        else
            errorMessage = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    // API 응답 매핑용 로컬 DTO
    private record PlayerItemDto(int ItemId, string ItemName, string ItemType, int Quantity);
}
