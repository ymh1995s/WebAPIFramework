using Framework.Application.DTOs;

namespace Framework.Application.Interfaces;

// 플레이어 인벤토리 서비스 인터페이스
public interface IPlayerItemService
{
    // 플레이어 보유 아이템 목록 조회
    Task<List<PlayerItemDto>> GetByPlayerIdAsync(int playerId);
}
