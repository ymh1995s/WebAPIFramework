using Framework.Application.DTOs;
using Framework.Application.Interfaces;

namespace Framework.Application.Services;

// 랭킹 서비스 구현체
public class RankingService : IRankingService
{
    private readonly IRankingRepository _rankingRepo;
    private readonly IPlayerRepository _playerRepo;

    public RankingService(IRankingRepository rankingRepo, IPlayerRepository playerRepo)
    {
        _rankingRepo = rankingRepo;
        _playerRepo = playerRepo;
    }

    // 상위 N명 랭킹 조회 - 순위 번호 부여 후 반환
    public async Task<List<RankingDto>> GetTopRankingsAsync(int count = 100)
    {
        var rankings = await _rankingRepo.GetTopRankingsAsync(count);

        return rankings
            .Select((r, index) => new RankingDto(index + 1, r.PlayerId, r.Nickname, r.BestScore))
            .ToList();
    }

    // 내 순위 조회
    public async Task<RankingDto> GetMyRankingAsync(int playerId)
    {
        var player = await _playerRepo.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");

        var rank = await _rankingRepo.GetPlayerRankAsync(playerId);
        var bestScore = await _rankingRepo.GetPlayerBestScoreAsync(playerId);

        return new RankingDto(rank, playerId, player.Nickname, bestScore);
    }
}
