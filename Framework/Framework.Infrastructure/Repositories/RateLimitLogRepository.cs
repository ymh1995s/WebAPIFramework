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

    /// <summary>Rate Limit 초과 로그를 DB에 기록한다</summary>
    public async Task AddAsync(RateLimitLog log)
    {
        _context.RateLimitLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
