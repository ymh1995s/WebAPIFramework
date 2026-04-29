using Framework.Domain.Entities;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Framework.Domain.Interfaces;

namespace Framework.Infrastructure.Repositories;

// 플레이어 저장소 구현체
public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;

    public PlayerRepository(AppDbContext db)
    {
        _db = db;
    }

    // DeviceId로 플레이어 조회 (Global Query Filter 적용 — 소프트 딜리트 계정 제외)
    public async Task<Player?> GetByDeviceIdAsync(string deviceId)
        => await _db.Players.FirstOrDefaultAsync(p => p.DeviceId == deviceId);

    // 전체 플레이어 조회 (Global Query Filter 적용 — 소프트 딜리트 계정 제외)
    public async Task<List<Player>> GetAllAsync()
        => await _db.Players.ToListAsync();

    // GoogleId로 플레이어 조회 (Global Query Filter 적용 — 소프트 딜리트 계정 제외)
    public async Task<Player?> GetByGoogleIdAsync(string googleId)
        => await _db.Players.FirstOrDefaultAsync(p => p.GoogleId == googleId);

    // Id로 플레이어 조회 (Global Query Filter 적용 — 소프트 딜리트 계정 제외)
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

    // DeviceId 또는 닉네임 부분 일치 검색 — Admin 플레이어 검색 기능에서 사용
    public async Task<List<Player>> SearchByKeywordAsync(string keyword)
    {
        var lower = keyword.ToLower();
        return await _db.Players
            .Where(p =>
                p.DeviceId.ToLower().Contains(lower) ||
                p.Nickname.ToLower().Contains(lower))
            .ToListAsync();
    }

    // 플레이어 밴 처리 — IsBanned=true, BannedUntil 설정
    public async Task BanAsync(int playerId, DateTime? bannedUntil)
    {
        // IgnoreQueryFilters: 밴 대상이 소프트 딜리트 계정일 수도 있으므로 필터 우회
        var player = await _db.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == playerId);
        if (player is null) return;

        player.IsBanned = true;
        player.BannedUntil = bannedUntil; // null이면 영구 밴
        await _db.SaveChangesAsync();
    }

    // 플레이어 밴 해제 — IsBanned=false, BannedUntil 초기화
    public async Task UnbanAsync(int playerId)
    {
        // IgnoreQueryFilters: 밴 해제 대상이 소프트 딜리트 계정일 수도 있으므로 필터 우회
        var player = await _db.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == playerId);
        if (player is null) return;

        player.IsBanned = false;
        player.BannedUntil = null;
        await _db.SaveChangesAsync();
    }

    // 플레이어 삭제 - CASCADE로 연관 데이터 전부 삭제됨
    // [260423 기준] Players 행 삭제 시 아래 테이블이 자동 삭제됨:
    //   - RefreshTokens         : 로그인 토큰
    //   - PlayerProfiles        : 레벨, 경험치, 재화
    //   - GameResultParticipants : 게임 결과 참가 기록/점수 (GameMatchParticipants에서 이름 변경)
    //   - PlayerItems           : 인벤토리 (보유 아이템 목록)
    //   - Mails                 : 받은 우편
    //   - DailyLoginLogs        : 일일 로그인 기록
    //   - RewardGrants          : 보상 지급 이력
    public async Task DeleteAsync(Player player)
    {
        _db.Players.Remove(player);
        await _db.SaveChangesAsync();
    }

    // 플레이어 소프트 딜리트 — IsDeleted=true, DeletedAt=UtcNow, MergedIntoPlayerId 설정
    // 계정 병합 시 게스트 계정을 논리적으로 삭제하되 데이터는 보존
    public async Task SoftDeleteAsync(Player player, int mergedIntoPlayerId)
    {
        player.IsDeleted = true;
        player.DeletedAt = DateTime.UtcNow;
        player.MergedIntoPlayerId = mergedIntoPlayerId;
        _db.Players.Update(player);
        await _db.SaveChangesAsync();
    }

    // 소프트 딜리트된 계정을 포함한 전체 플레이어 목록 조회 (Admin 전용)
    // IgnoreQueryFilters: Global Query Filter(IsDeleted = false)를 우회
    public async Task<List<Player>> GetAllIncludingDeletedAsync()
        => await _db.Players.IgnoreQueryFilters().ToListAsync();

    // 소프트 딜리트된 계정을 포함하여 키워드로 검색 (Admin 전용)
    public async Task<List<Player>> SearchByKeywordIncludingDeletedAsync(string keyword)
    {
        var lower = keyword.ToLower();
        return await _db.Players
            .IgnoreQueryFilters()
            .Where(p =>
                p.DeviceId.ToLower().Contains(lower) ||
                p.Nickname.ToLower().Contains(lower))
            .ToListAsync();
    }

    // 소프트 딜리트된 계정을 포함하여 ID로 조회 (Admin 전용)
    public async Task<Player?> GetByIdIncludingDeletedAsync(int id)
        => await _db.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);

    // 지정 플레이어들의 AttendanceCount를 1씩 일괄 증가 (ExecuteUpdate 배치)
    public async Task IncrementAttendanceCountAsync(IEnumerable<int> playerIds)
    {
        var idList = playerIds.ToList();
        if (idList.Count == 0) return;

        // EF Core ExecuteUpdate: 트래킹 없이 직접 UPDATE SQL 실행 — 메모리 효율적
        await _db.Players
            .Where(p => idList.Contains(p.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.AttendanceCount, p => p.AttendanceCount + 1));
    }
}
