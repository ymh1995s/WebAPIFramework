using Framework.Admin.Constants;
using Framework.Application.Features.Matchmaking;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 매칭 테스트 페이지 코드-비하인드.
/// SignalR을 통해 실시간 매칭 대기/성사 이벤트를 수신하고,
/// API를 통해 매칭 참가/취소를 수행한다.
/// IAsyncDisposable을 구현하여 페이지 이탈 시 허브 연결을 정리한다.
/// </summary>
public partial class MatchMaking : ComponentBase, IAsyncDisposable
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;

    private string userId = string.Empty;
    private Tier selectedTier = Tier.Tier5;
    private int waitingCount;
    private int maxPlayers = 4;
    private List<MatchUserDto> matchedMembers = [];
    private List<string> logs = [];
    private bool isBusy;
    private Tier? joinedTierGroup;
    private HubConnection? hubConnection;

    /// <summary>SignalR 허브 연결 상태 확인</summary>
    private bool IsConnected => hubConnection?.State == HubConnectionState.Connected;

    protected override async Task OnInitializedAsync()
    {
        // Api 서버의 SignalR 허브에 연결
        var baseUrl = Configuration["ApiBaseUrl"] ?? "https://localhost:7034";
        hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}{ApiRoutes.Hubs.Matchmaking}")
            .Build();

        // 대기 인원 변경 이벤트 수신
        hubConnection.On<int, int>("WaitingCountUpdated", (count, max) =>
        {
            waitingCount = count;
            maxPlayers = max;
            AddLog($"대기 인원 갱신: {count}/{max}");
            InvokeAsync(StateHasChanged);
        });

        // 매칭 성사 이벤트 수신
        hubConnection.On<MatchResultDto>("MatchComplete", result =>
        {
            matchedMembers = result.Members;
            AddLog($"매칭 성사: {result.Message}");
            InvokeAsync(StateHasChanged);
        });

        await hubConnection.StartAsync();
    }

    /// <summary>매칭 참가 - Tier 그룹 구독 후 API 호출</summary>
    private async Task JoinMatch()
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        isBusy = true;
        matchedMembers = [];

        // Tier가 변경됐으면 기존 그룹 탈퇴 후 새 그룹 참가
        if (joinedTierGroup != selectedTier)
        {
            if (joinedTierGroup.HasValue)
                await hubConnection!.InvokeAsync("LeaveTierGroup", joinedTierGroup.Value.ToString());
            await hubConnection!.InvokeAsync("JoinTierGroup", selectedTier.ToString());
            joinedTierGroup = selectedTier;
        }

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.PostAsJsonAsync(ApiRoutes.Matchmaking.Join, new JoinMatchRequestDto(userId, selectedTier));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<MatchResultDto>();
            if (result is not null) AddLog(result.Message);
        }
        else
        {
            AddLog($"오류: {response.StatusCode}");
        }

        isBusy = false;
    }

    /// <summary>매칭 취소 - API 호출</summary>
    private async Task CancelMatch()
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.DeleteAsync(ApiRoutes.Matchmaking.Cancel(userId));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<MatchResultDto>();
            if (result is not null) AddLog(result.Message);
        }
        else
        {
            AddLog($"취소 실패: {response.StatusCode}");
        }
    }

    private void AddLog(string message)
    {
        logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        // 최대 20개 유지
        if (logs.Count > 20) logs.RemoveAt(logs.Count - 1);
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
            await hubConnection.DisposeAsync();
    }
}
