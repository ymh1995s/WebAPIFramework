using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 일일 로그인 기록 저장소 구현체
public class DailyLoginLogRepository : IDailyLoginLogRepository
{
    private readonly AppDbContext _context;

    public DailyLoginLogRepository(AppDbContext context)
    {
        _context = context;
    }

    // 특정 플레이어가 해당 날짜에 이미 보상을 받았는지 확인
    public async Task<bool> ExistsAsync(int playerId, DateOnly date)
        => await _context.DailyLoginLogs.AnyAsync(l => l.PlayerId == playerId && l.LoginDate == date);

    // 로그인 기록 추가
    public async Task AddAsync(DailyLoginLog log)
        => await _context.DailyLoginLogs.AddAsync(log);

    // 다수 로그인 기록 일괄 추가 (배치 처리용)
    public async Task AddRangeAsync(IEnumerable<DailyLoginLog> logs)
        => await _context.DailyLoginLogs.AddRangeAsync(logs);

    // 특정 플레이어의 지정 연/월 로그인 횟수 조회 (이번 달 몇 번째 로그인인지 판단용)
    public async Task<int> CountByPlayerAndMonthAsync(int playerId, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return await _context.DailyLoginLogs
            .CountAsync(l => l.PlayerId == playerId && l.LoginDate >= start && l.LoginDate < end);
    }

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
