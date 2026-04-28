namespace Framework.Application.Features.Matchmaking;

// 매칭 알림 추상화 인터페이스 - 실제 구현은 Api 계층(SignalR)에서 담당
public interface IMatchNotifier
{
    // 매칭 성사 알림
    Task NotifyMatchedAsync(string tierGroup, MatchResultDto result);

    // 대기 인원 변경 알림
    Task NotifyWaitingAsync(string tierGroup, int waitingCount, int maxPlayers);
}
