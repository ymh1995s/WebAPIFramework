using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 튜토리얼 진행 상태 저장소 구현체
public class TutorialProgressRepository : ITutorialProgressRepository
{
    private readonly AppDbContext _context;

    public TutorialProgressRepository(AppDbContext context)
    {
        _context = context;
    }

    // 플레이어의 전체 진행 키-값 맵 조회
    public async Task<Dictionary<string, string>> GetAllByPlayerIdAsync(int playerId)
        => await _context.TutorialProgresses
            .Where(t => t.PlayerId == playerId)
            .ToDictionaryAsync(t => t.Key, t => t.Value);

    // 플레이어 행 수 카운트 — 상한 검사용
    public async Task<int> CountByPlayerIdAsync(int playerId)
        => await _context.TutorialProgresses
            .CountAsync(t => t.PlayerId == playerId);

    // 특정 플레이어+키 조합 조회 — upsert 판단용
    public async Task<TutorialProgress?> GetByPlayerAndKeyAsync(int playerId, string key)
        => await _context.TutorialProgresses
            .FirstOrDefaultAsync(t => t.PlayerId == playerId && t.Key == key);

    // 신규 진행 행 추가
    public async Task AddAsync(TutorialProgress progress)
        => await _context.TutorialProgresses.AddAsync(progress);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
