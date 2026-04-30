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

    // 기존 데이터 전체를 삭제하고 새 목록으로 교체 — 트랜잭션 내에서 원자적으로 처리
    public async Task ReplaceAllAsync(List<LevelThreshold> items)
    {
        // 트랜잭션 시작 — RemoveRange + AddRange + SaveChanges가 한 단위로 커밋/롤백
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 기존 전체 데이터 삭제
            var existing = await _context.LevelThresholds.ToListAsync();
            _context.LevelThresholds.RemoveRange(existing);

            // 새 데이터 삽입
            await _context.LevelThresholds.AddRangeAsync(items);

            // 변경 사항 저장
            await _context.SaveChangesAsync();

            // 트랜잭션 커밋
            await transaction.CommitAsync();
        }
        catch
        {
            // 오류 발생 시 롤백하여 데이터 일관성 보장
            await transaction.RollbackAsync();
            throw;
        }
    }
}
