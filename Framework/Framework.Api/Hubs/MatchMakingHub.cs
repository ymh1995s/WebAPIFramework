using Microsoft.AspNetCore.SignalR;

namespace Framework.Api.Hubs;

// SignalR 허브 - 클라이언트와 실시간 통신 담당
public class MatchMakingHub : Hub
{
    // 클라이언트가 특정 Tier 그룹에 참가 (해당 Tier 알림만 수신)
    public async Task JoinTierGroup(string tier) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, tier);

    // 클라이언트가 Tier 그룹에서 나가기
    public async Task LeaveTierGroup(string tier) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tier);
}
