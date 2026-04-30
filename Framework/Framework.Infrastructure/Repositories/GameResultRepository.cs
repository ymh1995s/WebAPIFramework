using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 게임 결과 저장소 구현체 — 랭킹 집계도 여기서 처리 (GameMatchRepository에서 이름 변경)
public class GameResultRepository : IGameResultRepository
{
    private readonly AppDbContext _db;

    public GameResultRepository(AppDbContext db)
    {
        _db = db;
    }

    // ID로 결과 단건 조회 (참가자 포함)
    public async Task<GameResult?> GetByIdAsync(Guid matchId)
        => await _db.GameResults
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == matchId);

    // ID로 결과 단건 조회 (참가자 + 플레이어 프로필 포함) — Admin 상세 조회용
    public async Task<GameResult?> GetByIdWithParticipantsAsync(Guid matchId)
        => await _db.GameResults
            .Include(m => m.Participants)
            .ThenInclude(p => p.Player)
            .ThenInclude(pl => pl.Profile)
            .FirstOrDefaultAsync(m => m.Id == matchId);

    // 새 게임 결과 추가
    public async Task AddAsync(GameResult match)
        => await _db.GameResults.AddAsync(match);

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

    // 특정 플레이어의 최고 점수 조회
    public async Task<int> GetPlayerBestScoreAsync(int playerId)
        => await _db.GameResultParticipants
            .Where(p => p.PlayerId == playerId && p.Score.HasValue)
            .MaxAsync(p => (int?)p.Score) ?? 0;

    // 특정 플레이어의 순위 조회 (내 최고점수보다 높은 플레이어 수 + 1)
    public async Task<int> GetPlayerRankAsync(int playerId)
    {
        var myBest = await GetPlayerBestScoreAsync(playerId);

        // 나보다 높은 최고점수 보유 플레이어 수 카운트
        var higherCount = await _db.GameResultParticipants
            .Where(p => p.Score.HasValue && p.HumanType == Domain.Enums.HumanType.Human)
            .GroupBy(p => p.PlayerId)
            .Select(g => g.Max(p => p.Score!.Value))
            .CountAsync(best => best > myBest);

        return higherCount + 1;
    }

    // Admin 필터 검색 — 결과 목록 페이지네이션 (Participants 전체 로딩 없이 COUNT 서브쿼리로 대체)
    public async Task<(List<(GameResult Match, int ParticipantCount)> Items, int TotalCount)> SearchAsync(
        Guid? matchId, int? playerId, Tier? tier, MatchState? state,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        // Include(m => m.Participants) 제거 — 과잉 로딩 방지
        var query = _db.GameResults
            .AsQueryable();

        // 특정 매치 ID 필터
        if (matchId.HasValue)
            query = query.Where(m => m.Id == matchId.Value);

        // 특정 플레이어 참가 매치 필터 — Participants 서브쿼리로 존재 여부 확인
        if (playerId.HasValue)
            query = query.Where(m => m.Participants.Any(p => p.PlayerId == playerId.Value));

        // Tier 필터
        if (tier.HasValue)
            query = query.Where(m => m.Tier == tier.Value);

        // State 필터
        if (state.HasValue)
            query = query.Where(m => m.State == state.Value);

        // 기간 필터 (StartedAt 기준, UTC)
        if (from.HasValue)
            query = query.Where(m => m.StartedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.StartedAt <= to.Value);

        var total = await query.CountAsync();

        // Participants 전체 로딩 없이 COUNT 서브쿼리로 참가자 수만 조회
        var raw = await query
            .OrderByDescending(m => m.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new { Match = m, ParticipantCount = m.Participants.Count() })
            .ToListAsync();

        var items = raw.Select(x => (x.Match, x.ParticipantCount)).ToList();
        return (items, total);
    }

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
