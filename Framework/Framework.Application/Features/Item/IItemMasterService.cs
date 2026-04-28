namespace Framework.Application.Features.Item;

// 아이템 마스터 관리 서비스 인터페이스 (Admin 전용)
public interface IItemMasterService
{
    // 전체 아이템 목록 조회
    Task<List<ItemDto>> GetAllAsync();
    // 아이템 생성
    Task<ItemDto> CreateAsync(CreateItemDto dto);
    // 아이템 수정
    Task UpdateAsync(int id, UpdateItemDto dto);
    // 보유 플레이어 수 조회
    Task<int> GetHolderCountAsync(int id);
    // 아이템 소프트 삭제
    Task DeleteAsync(int id);
}
