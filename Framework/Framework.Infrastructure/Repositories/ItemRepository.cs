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

    // 전체 아이템 마스터 조회
    public async Task<List<Item>> GetAllAsync()
        => await _context.Items.ToListAsync();

    // ID로 단건 조회
    public async Task<Item?> GetByIdAsync(int id)
        => await _context.Items.FindAsync(id);

    // 새 아이템 추가
    public async Task AddAsync(Item item)
        => await _context.Items.AddAsync(item);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
