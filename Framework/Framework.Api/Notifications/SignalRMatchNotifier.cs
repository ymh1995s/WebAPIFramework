using Framework.Application.Features.Matchmaking;
using Framework.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Framework.Api.Notifications;

// IMatchNotifier의 SignalR 구현체 - Api 계층에서 Application 인터페이스를 구현
public class SignalRMatchNotifier : IMatchNotifier
{
    private readonly IHubContext<MatchMakingHub> _hubContext;

    public SignalRMatchNotifier(IHubContext<MatchMakingHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // 매칭 성사 시 해당 Tier 그룹 전체에 알림
    public async Task NotifyMatchedAsync(string tierGroup, MatchResultDto result) =>
        await _hubContext.Clients.Group(tierGroup).SendAsync("MatchComplete", result);

    // 대기 인원 변경 시 해당 Tier 그룹 전체에 알림
    public async Task NotifyWaitingAsync(string tierGroup, int waitingCount, int maxPlayers) =>
        await _hubContext.Clients.Group(tierGroup).SendAsync("WaitingCountUpdated", waitingCount, maxPlayers);
}
