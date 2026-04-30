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

    // 프로필 추가 — SaveChanges는 호출자(Service)가 명시적으로 호출
    public async Task AddAsync(PlayerProfile profile)
    {
        await _db.PlayerProfiles.AddAsync(profile);
    }

    // 프로필 수정 — SaveChanges는 호출자(Service)가 명시적으로 호출
    public Task UpdateAsync(PlayerProfile profile)
    {
        _db.PlayerProfiles.Update(profile);
        return Task.CompletedTask;
    }

    // 변경사항을 DB에 반영 — 호출자(Service)가 명시적으로 호출
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
