using Framework.Domain.Entities;

// 플레이어 인게임 프로필 저장소 인터페이스
public interface IPlayerProfileRepository
{
    // PlayerId로 프로필 조회
    Task<PlayerProfile?> GetByPlayerIdAsync(int playerId);

    // 프로필 추가
    Task AddAsync(PlayerProfile profile);

    // 프로필 수정
    Task UpdateAsync(PlayerProfile profile);
}
