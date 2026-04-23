using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 감사 로그 저장소 구현체
public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log)
        => await _context.AuditLogs.AddAsync(log);

    // 필터 조건 중 null이 아닌 것만 where 절로 결합해서 검색
    public async Task<(List<AuditLog> items, int totalCount)> SearchAsync(
        int? playerId,
        int? itemId,
        DateTime? from,
        DateTime? to,
        bool? isAnomaly,
        int page,
        int pageSize)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (playerId.HasValue) query = query.Where(l => l.PlayerId == playerId.Value);
        if (itemId.HasValue) query = query.Where(l => l.ItemId == itemId.Value);
        if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(l => l.CreatedAt <= to.Value);
        if (isAnomaly.HasValue) query = query.Where(l => l.IsAnomaly == isAnomaly.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
