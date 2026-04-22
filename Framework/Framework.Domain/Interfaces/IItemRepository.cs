using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 아이템 저장소 인터페이스
public interface IItemRepository
{
    // 전체 아이템 마스터 조회 (삭제되지 않은 항목만)
    Task<List<Item>> GetAllAsync();
    // ID로 단건 조회
    Task<Item?> GetByIdAsync(int id);
    // 해당 아이템을 보유한 플레이어 수 조회
    Task<int> GetHolderCountAsync(int itemId);
    // 새 아이템 추가
    Task AddAsync(Item item);
    // 아이템 수정
    void Update(Item item);
    // 아이템 삭제
    Task DeleteAsync(int id);
    // 변경사항 저장
    Task SaveChangesAsync();
}
