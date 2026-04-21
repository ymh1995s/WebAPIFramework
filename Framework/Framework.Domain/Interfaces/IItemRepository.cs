using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 아이템 저장소 인터페이스
public interface IItemRepository
{
    // 전체 아이템 마스터 조회
    Task<List<Item>> GetAllAsync();
    // ID로 단건 조회
    Task<Item?> GetByIdAsync(int id);
    // 새 아이템 추가
    Task AddAsync(Item item);
    // 변경사항 저장
    Task SaveChangesAsync();
}
