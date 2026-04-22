using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 아이템 저장소 구현체
public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _context;

    public ItemRepository(AppDbContext context)
    {
        _context = context;
    }

    // 전체 아이템 마스터 조회 (삭제되지 않은 항목만)
    public async Task<List<Item>> GetAllAsync()
        => await _context.Items.Where(i => !i.IsDeleted).ToListAsync();

    // 해당 아이템을 보유한 플레이어 수 조회
    public async Task<int> GetHolderCountAsync(int itemId)
        => await _context.PlayerItems.CountAsync(pi => pi.ItemId == itemId);

    // ID로 단건 조회
    public async Task<Item?> GetByIdAsync(int id)
        => await _context.Items.FindAsync(id);

    // 새 아이템 추가
    public async Task AddAsync(Item item)
        => await _context.Items.AddAsync(item);

    // 아이템 수정 (EF Core 변경 추적)
    public void Update(Item item)
        => _context.Items.Update(item);

    // 아이템 삭제
    public async Task DeleteAsync(int id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item != null)
            _context.Items.Remove(item);
    }

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
