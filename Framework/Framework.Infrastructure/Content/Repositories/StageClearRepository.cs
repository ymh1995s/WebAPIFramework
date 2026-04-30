// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Domain.Content.Entities;
using Framework.Domain.Content.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Content.Repositories;

// 플레이어 스테이지 클리어 기록 저장소 구현체
public class StageClearRepository : IStageClearRepository
{
    private readonly AppDbContext _db;

    public StageClearRepository(AppDbContext db)
    {
        _db = db;
    }

    // PlayerId + StageId로 클리어 기록 조회 (없으면 null)
    public async Task<StageClear?> FindAsync(int playerId, int stageId)
        => await _db.StageClears
            .FirstOrDefaultAsync(c => c.PlayerId == playerId && c.StageId == stageId);

    // 특정 플레이어의 전체 클리어 기록 조회
    public async Task<List<StageClear>> GetByPlayerIdAsync(int playerId)
        => await _db.StageClears
            .Where(c => c.PlayerId == playerId)
            .ToListAsync();

    // 클리어 기록 추가
    public async Task AddAsync(StageClear stageClear)
        => await _db.StageClears.AddAsync(stageClear);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
