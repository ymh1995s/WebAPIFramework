using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

// 아이템 마스터 관리 서비스 구현체
public class ItemMasterService : IItemMasterService
{
    private readonly IItemRepository _itemRepository;

    public ItemMasterService(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    // 전체 아이템 목록 조회
    public async Task<List<ItemDto>> GetAllAsync()
    {
        var items = await _itemRepository.GetAllAsync();
        return items.Select(i => new ItemDto(i.Id, i.Name, i.ItemType, i.Description, i.AuditLevel, i.AnomalyThreshold)).ToList();
    }

    // 새 아이템 생성
    public async Task<ItemDto> CreateAsync(CreateItemDto dto)
    {
        var item = new Item
        {
            Name = dto.Name,
            ItemType = dto.ItemType,
            Description = dto.Description,
            AuditLevel = dto.AuditLevel,
            AnomalyThreshold = dto.AnomalyThreshold
        };
        await _itemRepository.AddAsync(item);
        await _itemRepository.SaveChangesAsync();
        return new ItemDto(item.Id, item.Name, item.ItemType, item.Description, item.AuditLevel, item.AnomalyThreshold);
    }

    // 아이템 수정
    public async Task UpdateAsync(int id, UpdateItemDto dto)
    {
        var item = await _itemRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"아이템 ID {id}를 찾을 수 없습니다.");

        item.Name = dto.Name;
        item.ItemType = dto.ItemType;
        item.Description = dto.Description;
        item.AuditLevel = dto.AuditLevel;
        item.AnomalyThreshold = dto.AnomalyThreshold;

        _itemRepository.Update(item);
        await _itemRepository.SaveChangesAsync();
    }

    // 보유 플레이어 수 조회
    public async Task<int> GetHolderCountAsync(int id)
        => await _itemRepository.GetHolderCountAsync(id);

    // 아이템 소프트 삭제 (IsDeleted = true) — 행을 DB에서 제거하지 않고 플래그만 변경. 복구 가능
    // 반대 개념인 하드 삭제는 DB에서 행 자체를 DELETE하여 완전히 제거. 복구 불가
    public async Task DeleteAsync(int id)
    {
        var item = await _itemRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"아이템 ID {id}를 찾을 수 없습니다.");

        item.IsDeleted = true;
        _itemRepository.Update(item);
        await _itemRepository.SaveChangesAsync();
    }
}
