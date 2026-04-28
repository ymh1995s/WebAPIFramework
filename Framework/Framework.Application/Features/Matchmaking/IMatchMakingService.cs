namespace Framework.Application.Features.Matchmaking;

// 매칭 서비스 인터페이스 - 실제 프로젝트에서 로직을 오버라이드하거나 교체
public interface IMatchMakingService
{
    // 매칭 대기열 참가
    Task<MatchResultDto> JoinAsync(JoinMatchRequestDto request);

    // 매칭 취소
    Task<MatchResultDto?> CancelAsync(string userId);
}
