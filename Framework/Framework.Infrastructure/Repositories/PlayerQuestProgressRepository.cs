using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 플레이어 퀘스트 진행 상태 저장소 구현체
public class PlayerQuestProgressRepository : IPlayerQuestProgressRepository
{
    private readonly AppDbContext _context;

    public PlayerQuestProgressRepository(AppDbContext context)
    {
        _context = context;
    }

    // PlayerId + QuestId + PeriodKey 조합으로 단건 조회
    public async Task<PlayerQuestProgress?> FindAsync(int playerId, int questId, string periodKey)
        => await _context.PlayerQuestProgresses
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.QuestId == questId && p.PeriodKey == periodKey);

    // 플레이어의 특정 주기 퀘스트 진행 상태 전체 조회 — 클라이언트 목록용
    public async Task<List<PlayerQuestProgress>> GetByPlayerAndPeriodKeyAsync(int playerId, string periodKey)
        => await _context.PlayerQuestProgresses
            .Where(p => p.PlayerId == playerId && p.PeriodKey == periodKey)
            .ToListAsync();

    // 플레이어의 특정 퀘스트 진행 상태 전체 조회 (여러 PeriodKey)
    public async Task<List<PlayerQuestProgress>> GetByPlayerAndQuestAsync(int playerId, int questId)
        => await _context.PlayerQuestProgresses
            .Where(p => p.PlayerId == playerId && p.QuestId == questId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    // 진행 상태 추가
    public async Task AddAsync(PlayerQuestProgress progress)
        => await _context.PlayerQuestProgresses.AddAsync(progress);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
