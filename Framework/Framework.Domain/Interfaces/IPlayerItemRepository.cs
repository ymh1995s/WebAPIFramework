using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 플레이어 인벤토리 저장소 인터페이스
public interface IPlayerItemRepository
{
    // 플레이어 보유 아이템 전체 조회
    Task<List<PlayerItem>> GetByPlayerIdAsync(int playerId);
    // 특정 플레이어가 특정 아이템을 보유 중인지 조회 (수령 시 수량 증가 여부 판단용)
    Task<PlayerItem?> GetByPlayerAndItemAsync(int playerId, int itemId);
    // 특정 플레이어가 보유한 복수 아이템 배치 조회 (우편 수령 N+1 방지용)
    Task<List<PlayerItem>> GetByPlayerAndItemIdsAsync(int playerId, List<int> itemIds);
    // 새 인벤토리 행 추가
    Task AddAsync(PlayerItem playerItem);
    // 변경사항 저장
    Task SaveChangesAsync();
}
