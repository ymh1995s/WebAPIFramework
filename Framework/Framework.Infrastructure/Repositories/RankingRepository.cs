using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 랭킹 저장소 구현체 — GameResultParticipants 기반 집계 (GameMatchParticipants에서 이름 변경)
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
        return await _db.GameResultParticipants
            .Where(p => p.Score.HasValue && p.HumanType == Domain.Enums.HumanType.Human)
            .GroupBy(p => new { p.PlayerId, p.Player.PublicId, p.Player.Nickname })
            .Select(g => new
            {
                g.Key.PlayerId,
                g.Key.PublicId,
                g.Key.Nickname,
                BestScore = g.Max(p => p.Score!.Value)
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
        var higherCount = await _db.GameResultParticipants
            .Where(p => p.Score.HasValue && p.HumanType == Domain.Enums.HumanType.Human)
            .GroupBy(p => p.PlayerId)
            .Select(g => g.Max(p => p.Score!.Value))
            .CountAsync(best => best > myBest);

        return higherCount + 1;
    }

    // 특정 플레이어의 최고 점수 조회
    public async Task<int> GetPlayerBestScoreAsync(int playerId)
        => await _db.GameResultParticipants
            .Where(p => p.PlayerId == playerId && p.Score.HasValue)
            .MaxAsync(p => (int?)p.Score) ?? 0;
}
