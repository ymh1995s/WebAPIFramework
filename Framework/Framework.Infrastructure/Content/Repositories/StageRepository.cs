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

// 스테이지 마스터 저장소 구현체
public class StageRepository : IStageRepository
{
    private readonly AppDbContext _db;

    public StageRepository(AppDbContext db)
    {
        _db = db;
    }

    // ID로 스테이지 조회 (비활성 포함 — 관리 목적)
    public async Task<Stage?> GetByIdAsync(int id)
        => await _db.Stages.FirstOrDefaultAsync(s => s.Id == id);

    // Code로 스테이지 조회
    public async Task<Stage?> GetByCodeAsync(string code)
        => await _db.Stages.FirstOrDefaultAsync(s => s.Code == code);

    // 활성 스테이지 전체 조회 (SortOrder 오름차순)
    public async Task<List<Stage>> GetAllActiveAsync()
        => await _db.Stages
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

    // Admin 검색 — 키워드(Code/Name) + 페이지네이션
    public async Task<(List<Stage> Items, int TotalCount)> SearchAsync(
        string? keyword, int page, int pageSize)
    {
        var query = _db.Stages.AsQueryable();

        // 키워드 필터 — Code 또는 Name 부분 일치
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(s =>
                s.Code.Contains(keyword) ||
                s.Name.Contains(keyword));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // 스테이지 추가
    public async Task AddAsync(Stage stage)
        => await _db.Stages.AddAsync(stage);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
