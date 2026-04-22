using Framework.Domain.Entities;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 플레이어 인게임 프로필 저장소 구현체
public class PlayerProfileRepository : IPlayerProfileRepository
{
    private readonly AppDbContext _db;

    public PlayerProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    // PlayerId로 프로필 조회
    public async Task<PlayerProfile?> GetByPlayerIdAsync(int playerId)
        => await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId);

    // 프로필 추가
    public async Task AddAsync(PlayerProfile profile)
    {
        await _db.PlayerProfiles.AddAsync(profile);
        await _db.SaveChangesAsync();
    }

    // 프로필 수정
    public async Task UpdateAsync(PlayerProfile profile)
    {
        _db.PlayerProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }
}
