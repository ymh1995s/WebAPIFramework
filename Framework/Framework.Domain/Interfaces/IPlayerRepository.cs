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

    // 전체 플레이어 조회 (일괄 우편 발송 시 사용)
    Task<List<Player>> GetAllAsync();

    // 플레이어 추가
    Task AddAsync(Player player);

    // 플레이어 수정 (LastLoginAt 갱신 등)
    Task UpdateAsync(Player player);

    // DeviceId 또는 닉네임 부분 일치 검색 (Admin 전용)
    Task<List<Player>> SearchByKeywordAsync(string keyword);

    // 플레이어 밴 처리 (bannedUntil: null이면 영구 밴, 값이 있으면 기간 밴)
    Task BanAsync(int playerId, DateTime? bannedUntil);

    // 플레이어 밴 해제
    Task UnbanAsync(int playerId);

    // 플레이어 삭제 (계정 탈퇴 - 연관 데이터 CASCADE 삭제)
    Task DeleteAsync(Player player);
}
