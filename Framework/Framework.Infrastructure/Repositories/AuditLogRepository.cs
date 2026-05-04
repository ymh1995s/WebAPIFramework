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

    // Timeline용 이상치 감사 로그 조회 — IsAnomaly=true 강제, 기간 + playerId 필터, 최대 take건
    // SearchAsync와 달리 페이지네이션 없음 (Timeline 전용 단순 조회)
    public async Task<List<AuditLog>> GetRecentAnomaliesAsync(
        DateTime fromUtc, DateTime toUtc, int? playerId, int take)
    {
        // IsAnomaly=true 강제 + 기간 필터
        var query = _context.AuditLogs
            .Where(l => l.IsAnomaly && l.CreatedAt >= fromUtc && l.CreatedAt <= toUtc);

        // 선택 필터 — null이면 적용 안 함
        if (playerId.HasValue)
            query = query.Where(l => l.PlayerId == playerId.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
