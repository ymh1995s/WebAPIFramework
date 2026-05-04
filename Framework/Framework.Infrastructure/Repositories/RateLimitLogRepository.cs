using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// Rate Limit 초과 로그 저장소 구현체
// IRateLimitLogRepository 인터페이스 구현 — M-5/L-23 해소
public class RateLimitLogRepository : IRateLimitLogRepository
{
    private readonly AppDbContext _context;

    public RateLimitLogRepository(AppDbContext context)
    {
        _context = context;
    }

    // Rate Limit 초과 로그를 변경 추적에 등록 — 실제 저장은 SaveChangesAsync로 처리
    public Task AddAsync(RateLimitLog log)
    {
        // 변경 추적만 등록 — 저장은 호출자(OnRejected 콜백)가 명시적으로 처리
        _context.RateLimitLogs.Add(log);
        return Task.CompletedTask;
    }

    // 변경 사항을 DB에 저장 — 호출자가 SaveChanges 시점을 명시적으로 제어
    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    // IP별 Rate Limit 집계 — Admin 집계 화면용 (Count 내림차순 정렬)
    public async Task<List<(string IpAddress, int Count, DateTime LastOccurredAt)>> GetAggregatedByIpAsync()
    {
        // GroupBy → Select 튜플 변환 → 내림차순 정렬
        var result = await _context.RateLimitLogs
            .GroupBy(l => l.IpAddress)
            .Select(g => new
            {
                IpAddress = g.Key,
                Count = g.Count(),
                LastOccurredAt = g.Max(l => l.OccurredAt)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        // 익명 타입 → 튜플 변환 (EF Core가 튜플 직접 프로젝션 미지원)
        return result
            .Select(x => (x.IpAddress, x.Count, x.LastOccurredAt))
            .ToList();
    }

    // 기간 + 선택 필터(playerId/ip)로 최근 N건 조회 — Admin Timeline (OccurredAt 내림차순)
    public async Task<List<RateLimitLog>> GetRecentByFiltersAsync(
        DateTime fromUtc, DateTime toUtc, int? playerId, string? ipAddress, int take)
    {
        // 기간 필터 적용
        var query = _context.RateLimitLogs
            .Where(l => l.OccurredAt >= fromUtc && l.OccurredAt <= toUtc);

        // 선택 필터 — null이면 적용 안 함
        if (playerId.HasValue)
            query = query.Where(l => l.PlayerId == playerId);
        if (!string.IsNullOrEmpty(ipAddress))
            query = query.Where(l => l.IpAddress == ipAddress);

        return await query
            .OrderByDescending(l => l.OccurredAt)
            .Take(take)
            .ToListAsync();
    }
}
