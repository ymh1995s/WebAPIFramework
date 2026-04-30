using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 플레이어 인벤토리 저장소 구현체
public class PlayerItemRepository : IPlayerItemRepository
{
    private readonly AppDbContext _context;

    public PlayerItemRepository(AppDbContext context)
    {
        _context = context;
    }

    // 플레이어 보유 아이템 전체 조회 (Item 네비게이션 프로퍼티 포함 로드)
    public async Task<List<PlayerItem>> GetByPlayerIdAsync(int playerId)
        => await _context.PlayerItems
            .Include(pi => pi.Item)
            .Where(pi => pi.PlayerId == playerId)
            .ToListAsync();

    // 특정 플레이어+아이템 조합 조회 (수령 시 수량 증가 여부 판단용)
    public async Task<PlayerItem?> GetByPlayerAndItemAsync(int playerId, int itemId)
        => await _context.PlayerItems
            .FirstOrDefaultAsync(pi => pi.PlayerId == playerId && pi.ItemId == itemId);

    // 배치 IN 쿼리 — MailItems 수령 시 N+1 방지
    public async Task<List<PlayerItem>> GetByPlayerAndItemIdsAsync(int playerId, List<int> itemIds)
        => await _context.PlayerItems
            .Where(pi => pi.PlayerId == playerId && itemIds.Contains(pi.ItemId))
            .ToListAsync();

    // 새 인벤토리 행 추가
    public async Task AddAsync(PlayerItem playerItem)
        => await _context.PlayerItems.AddAsync(playerItem);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
