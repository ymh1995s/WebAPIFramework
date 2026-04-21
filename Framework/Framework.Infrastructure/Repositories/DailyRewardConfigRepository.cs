using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 일일 보상 설정 저장소 구현체
public class DailyRewardConfigRepository : IDailyRewardConfigRepository
{
    private readonly AppDbContext _context;

    public DailyRewardConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DailyRewardConfig?> GetByDayAsync(int day)
        => await _context.DailyRewardConfigs.FirstOrDefaultAsync(c => c.Day == day);

    // 연속일수 무관 기본 보상 (Day=1 단일 설정)
    public async Task<DailyRewardConfig?> GetDefaultAsync()
        => await _context.DailyRewardConfigs.OrderBy(c => c.Day).FirstOrDefaultAsync();

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
