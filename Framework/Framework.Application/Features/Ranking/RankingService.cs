using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Ranking;

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

    // 상위 N명 랭킹 조회 - 순위 번호 부여 후 반환 (외부 공개용 PublicId 사용)
    public async Task<List<RankingDto>> GetTopRankingsAsync(int count = 100)
    {
        var rankings = await _rankingRepo.GetTopRankingsAsync(count);

        // PlayerId 자리에 외부 공개용 PublicId(Guid) 사용
        return rankings
            .Select((r, index) => new RankingDto(index + 1, r.PublicId, r.Nickname, r.BestScore))
            .ToList();
    }

    // 내 순위 조회 — 플레이어의 PublicId를 DTO에 포함
    public async Task<RankingDto> GetMyRankingAsync(int playerId)
    {
        var player = await _playerRepo.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");

        var rank = await _rankingRepo.GetPlayerRankAsync(playerId);
        var bestScore = await _rankingRepo.GetPlayerBestScoreAsync(playerId);

        // 외부 공개용 PublicId 사용 (내부 정수 Id 미노출)
        return new RankingDto(rank, player.PublicId, player.Nickname, bestScore);
    }
}
