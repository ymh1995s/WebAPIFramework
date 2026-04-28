using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 랭킹 저장소 구현체 - PlayerRecord와 Player 조인으로 집계
public class RankingRepository : IRankingRepository
{
    private readonly AppDbContext _db;

    public RankingRepository(AppDbContext db)
    {
        _db = db;
    }

    // 플레이어별 최고 점수 기준 상위 N명 조회 — PublicId 포함 (내부 Id 직접 반환 금지)
    public async Task<List<(int PlayerId, Guid PublicId, string Nickname, int BestScore)>> GetTopRankingsAsync(int count)
    {
        return await _db.PlayerRecords
            .GroupBy(r => new { r.PlayerId, r.Player.PublicId, r.Player.Nickname })
            .Select(g => new
            {
                g.Key.PlayerId,
                g.Key.PublicId,
                g.Key.Nickname,
                BestScore = g.Max(r => r.Score)
            })
            .OrderByDescending(x => x.BestScore)
            .Take(count)
            .Select(x => ValueTuple.Create(x.PlayerId, x.PublicId, x.Nickname, x.BestScore))
            .ToListAsync();
    }

    // 특정 플레이어의 순위 조회 (내 최고점수보다 높은 플레이어 수 + 1)
    public async Task<int> GetPlayerRankAsync(int playerId)
    {
        var myBest = await GetPlayerBestScoreAsync(playerId);

        // 나보다 높은 최고점수를 가진 플레이어 수를 구해 순위 계산
        var higherCount = await _db.PlayerRecords
            .GroupBy(r => r.PlayerId)
            .Select(g => g.Max(r => r.Score))
            .CountAsync(best => best > myBest);

        return higherCount + 1;
    }

    // 특정 플레이어의 최고 점수 조회
    public async Task<int> GetPlayerBestScoreAsync(int playerId)
    {
        return await _db.PlayerRecords
            .Where(r => r.PlayerId == playerId)
            .MaxAsync(r => (int?)r.Score) ?? 0;
    }
}
