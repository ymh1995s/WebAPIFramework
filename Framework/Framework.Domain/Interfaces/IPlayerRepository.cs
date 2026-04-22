using Framework.Domain.Entities;

// 플레이어 저장소 인터페이스
public interface IPlayerRepository
{
    // DeviceId로 플레이어 조회
    Task<Player?> GetByDeviceIdAsync(string deviceId);

    // GoogleId로 플레이어 조회
    Task<Player?> GetByGoogleIdAsync(string googleId);

    // Id로 플레이어 조회
    Task<Player?> GetByIdAsync(int id);

    // 플레이어 추가
    Task AddAsync(Player player);

    // 플레이어 수정 (LastLoginAt 갱신 등)
    Task UpdateAsync(Player player);
}
