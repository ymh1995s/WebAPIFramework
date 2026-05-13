using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 퀘스트 정의 저장소 구현체
public class QuestDefinitionRepository : IQuestDefinitionRepository
{
    private readonly AppDbContext _context;

    public QuestDefinitionRepository(AppDbContext context)
    {
        _context = context;
    }

    // ID로 단건 조회 — 소프트 삭제 포함 전체 대상 (Admin용)
    public async Task<QuestDefinition?> GetByIdAsync(int id)
        => await _context.QuestDefinitions.FindAsync(id);

    // 활성 퀘스트 정의 전체 조회 — IMemoryCache 캐시 갱신용
    public async Task<List<QuestDefinition>> GetActiveListAsync()
        => await _context.QuestDefinitions
            .Where(q => q.IsActive && !q.IsDeleted)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

    // 주기별 활성 퀘스트 조회 — 클라이언트 목록 표시용
    public async Task<List<QuestDefinition>> GetActiveByPeriodAsync(QuestPeriod period)
        => await _context.QuestDefinitions
            .Where(q => q.Period == period && q.IsActive && !q.IsDeleted)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

    // Admin 검색 — 키워드 + 주기 필터 + 페이지네이션
    public async Task<(List<QuestDefinition> Items, int TotalCount)> SearchAsync(
        string? keyword, QuestPeriod? period, bool? isActive, int page, int pageSize)
    {
        // 소프트 삭제 포함 전체 조회 (Admin은 삭제된 항목도 볼 수 있어야 함)
        var query = _context.QuestDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(q => q.Title.Contains(keyword) || q.Code.Contains(keyword));

        if (period.HasValue)
            query = query.Where(q => q.Period == period.Value);

        if (isActive.HasValue)
            query = query.Where(q => q.IsActive == isActive.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(q => q.SortOrder)
            .ThenByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // 퀘스트 정의 추가
    public async Task AddAsync(QuestDefinition quest)
        => await _context.QuestDefinitions.AddAsync(quest);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
