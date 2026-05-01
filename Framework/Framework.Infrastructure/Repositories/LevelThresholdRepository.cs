using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 레벨 임계값 저장소 구현 — EF Core 기반 조회 및 일괄 교체
public class LevelThresholdRepository : ILevelThresholdRepository
{
    private readonly AppDbContext _context;

    public LevelThresholdRepository(AppDbContext context)
    {
        _context = context;
    }

    // 모든 레벨 임계값을 레벨 오름차순으로 조회
    public async Task<List<LevelThreshold>> GetAllOrderedAsync()
    {
        return await _context.LevelThresholds
            .OrderBy(t => t.Level)
            .ToListAsync();
    }

    // 기존 데이터 전체를 변경 추적에서 삭제하고 새 목록을 추가 — 트랜잭션/저장은 호출자(IUnitOfWork)가 처리
    public async Task ReplaceAllAsync(List<LevelThreshold> items)
    {
        // 기존 전체 데이터 삭제 (변경 추적 등록)
        var existing = await _context.LevelThresholds.ToListAsync();
        _context.LevelThresholds.RemoveRange(existing);

        // 새 데이터 삽입 (변경 추적 등록)
        await _context.LevelThresholds.AddRangeAsync(items);
    }

    // 변경 사항을 DB에 저장 — 호출자가 SaveChanges 시점을 명시적으로 제어
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
