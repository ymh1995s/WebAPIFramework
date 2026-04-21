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

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
