using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Item;

// 플레이어 인벤토리 서비스 구현체
public class PlayerItemService : IPlayerItemService
{
    private readonly IPlayerItemRepository _repository;

    public PlayerItemService(IPlayerItemRepository repository)
    {
        _repository = repository;
    }

    // 플레이어 보유 아이템 목록을 DTO로 변환하여 반환
    // 통화 아이템(ItemType.Currency)도 인벤토리에 포함 — Gold/Gems는 PlayerItem에서 관리
    public async Task<List<PlayerItemDto>> GetByPlayerIdAsync(int playerId)
    {
        var items = await _repository.GetByPlayerIdAsync(playerId);
        return items.Select(pi => new PlayerItemDto(pi.ItemId, pi.Item.Name, pi.Item.ItemType, pi.Quantity)).ToList();
    }
}
