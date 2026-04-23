using Framework.Domain.Entities;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 플레이어 저장소 구현체
public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;

    public PlayerRepository(AppDbContext db)
    {
        _db = db;
    }

    // DeviceId로 플레이어 조회
    public async Task<Player?> GetByDeviceIdAsync(string deviceId)
        => await _db.Players.FirstOrDefaultAsync(p => p.DeviceId == deviceId);

    // 전체 플레이어 조회
    public async Task<List<Player>> GetAllAsync()
        => await _db.Players.ToListAsync();

    // GoogleId로 플레이어 조회
    public async Task<Player?> GetByGoogleIdAsync(string googleId)
        => await _db.Players.FirstOrDefaultAsync(p => p.GoogleId == googleId);

    // Id로 플레이어 조회
    public async Task<Player?> GetByIdAsync(int id)
        => await _db.Players.FindAsync(id);

    // 신규 플레이어 추가
    public async Task AddAsync(Player player)
    {
        await _db.Players.AddAsync(player);
        await _db.SaveChangesAsync();
    }

    // 플레이어 정보 수정
    public async Task UpdateAsync(Player player)
    {
        _db.Players.Update(player);
        await _db.SaveChangesAsync();
    }

    // 플레이어 삭제 - CASCADE로 연관 데이터 전부 삭제됨
    // [260423 기준] Players 행 삭제 시 아래 테이블이 자동 삭제됨:
    //   - RefreshTokens    : 로그인 토큰
    //   - PlayerProfiles   : 레벨, 경험치, 재화
    //   - PlayerRecords    : 게임 플레이 기록/점수
    //   - PlayerItems      : 인벤토리 (보유 아이템 목록)
    //   - Mails            : 받은 우편
    //   - DailyLoginLogs   : 일일 로그인 기록
    public async Task DeleteAsync(Player player)
    {
        _db.Players.Remove(player);
        await _db.SaveChangesAsync();
    }
}
