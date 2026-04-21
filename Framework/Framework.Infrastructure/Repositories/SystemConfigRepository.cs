using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 시스템 설정 저장소 구현체
public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly AppDbContext _context;

    public SystemConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    // Key에 해당하는 설정값 조회, 없으면 null 반환
    public async Task<string?> GetValueAsync(string key)
    {
        var config = await _context.SystemConfigs.FindAsync(key);
        return config?.Value;
    }

    // Key에 값 저장 (없으면 INSERT, 있으면 UPDATE)
    public async Task SetValueAsync(string key, string value)
    {
        var config = await _context.SystemConfigs.FindAsync(key);
        if (config is null)
            await _context.SystemConfigs.AddAsync(new SystemConfig { Key = key, Value = value });
        else
            config.Value = value;
    }

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
