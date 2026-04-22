using Framework.Api.Filters;
using Framework.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Framework.Api.Controllers;

// Admin 전용 Rate Limit 로그 조회 컨트롤러 - X-Admin-Key 헤더 검증 자동 적용
[AdminApiKey]
[ApiController]
[Route("api/admin/rate-limit-logs")]
public class AdminRateLimitLogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminRateLimitLogsController(AppDbContext context)
    {
        _context = context;
    }

    // IP별로 집계한 Rate Limit 초과 현황 반환 — 횟수 많은 순 정렬
    [HttpGet]
    public async Task<IActionResult> GetLogs()
    {
        var logs = await _context.RateLimitLogs
            .GroupBy(l => l.IpAddress)
            .Select(g => new
            {
                IpAddress = g.Key,
                Count = g.Count(),
                LastOccurredAt = g.Max(l => l.OccurredAt)
            })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        return Ok(logs);
    }
}
