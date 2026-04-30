using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 보상 테이블 저장소 구현체
public class RewardTableRepository : IRewardTableRepository
{
    private readonly AppDbContext _db;

    public RewardTableRepository(AppDbContext db)
    {
        _db = db;
    }

    // SourceType + Code 조합으로 보상 테이블 조회 (항목 포함, 삭제된 것 제외)
    public async Task<RewardTable?> FindAsync(RewardSourceType sourceType, string code)
        => await _db.RewardTables
            .Include(t => t.Entries)
            .ThenInclude(e => e.Item)
            .FirstOrDefaultAsync(t => t.SourceType == sourceType && t.Code == code && !t.IsDeleted);

    // 전체 보상 테이블 조회 (소프트 딜리트 제외)
    public async Task<List<RewardTable>> GetAllAsync()
        => await _db.RewardTables
            .Include(t => t.Entries)
            .Where(t => !t.IsDeleted)
            .ToListAsync();

    // Admin 필터 검색 — sourceType, code 부분 일치 + 페이지네이션
    // Entries 전체 로딩 대신 서브쿼리 COUNT 프로젝션으로 과잉 로딩 방지
    public async Task<(List<(RewardTable Table, int EntriesCount)> Items, int TotalCount)> SearchAsync(
        RewardSourceType? sourceType, string? code, int page, int pageSize)
    {
        // 소프트 딜리트된 항목 제외
        var query = _db.RewardTables
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        // SourceType 필터
        if (sourceType.HasValue)
            query = query.Where(t => t.SourceType == sourceType.Value);

        // Code 부분 일치 필터
        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(t => t.Code.Contains(code));

        var total = await query.CountAsync();

        // Entries를 전체 로딩하지 않고 COUNT 서브쿼리만 실행 — 과잉 로딩 방지
        var raw = await query
            .OrderBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new { Table = t, EntriesCount = t.Entries.Count() })
            .ToListAsync();

        var items = raw.Select(x => (x.Table, x.EntriesCount)).ToList();
        return (items, total);
    }

    // ID로 단건 조회 (Entries 포함, 소프트 딜리트 포함 — 관리 목적)
    public async Task<RewardTable?> GetByIdWithEntriesAsync(int id)
        => await _db.RewardTables
            .Include(t => t.Entries)
            .ThenInclude(e => e.Item)
            .FirstOrDefaultAsync(t => t.Id == id);

    // 새 보상 테이블 추가
    public async Task AddAsync(RewardTable table)
        => await _db.RewardTables.AddAsync(table);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
