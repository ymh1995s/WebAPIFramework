using Framework.Domain.Entities;
using Framework.Infrastructure.Persistence;

namespace Framework.Infrastructure.Repositories;

/// <summary>
/// Rate Limit 초과 로그 저장소
/// </summary>
public class RateLimitLogRepository
{
    private readonly AppDbContext _context;

    public RateLimitLogRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Rate Limit 초과 로그를 변경 추적에 등록한다 — 실제 저장은 SaveChangesAsync로 처리</summary>
    public Task AddAsync(RateLimitLog log)
    {
        // 변경 추적만 등록 — 저장은 호출자(OnRejected 콜백)가 명시적으로 처리
        _context.RateLimitLogs.Add(log);
        return Task.CompletedTask;
    }

    // 변경 사항을 DB에 저장 — 호출자가 SaveChanges 시점을 명시적으로 제어
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
