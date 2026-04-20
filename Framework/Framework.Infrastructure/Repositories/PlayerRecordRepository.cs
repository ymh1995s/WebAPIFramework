using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 플레이어 기록 저장소 구현체 (EF Core 기반)
public class PlayerRecordRepository : IPlayerRecordRepository
{
    private readonly AppDbContext _context;

    public PlayerRecordRepository(AppDbContext context)
    {
        _context = context;
    }

    // 전체 목록 조회
    public async Task<List<PlayerRecord>> GetAllAsync()
        => await _context.PlayerRecords.ToListAsync();

    // ID로 단건 조회
    public async Task<PlayerRecord?> GetByIdAsync(int id)
        => await _context.PlayerRecords.FindAsync(id);

    // 새 기록 추가
    public async Task AddAsync(PlayerRecord record)
        => await _context.PlayerRecords.AddAsync(record);

    // 변경사항 DB에 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
