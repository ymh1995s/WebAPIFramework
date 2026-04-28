using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 일일 보상 슬롯 저장소 구현체 (EF Core)
public class DailyRewardSlotRepository : IDailyRewardSlotRepository
{
    private readonly AppDbContext _context;

    public DailyRewardSlotRepository(AppDbContext context)
    {
        _context = context;
    }

    // 슬롯 전체 28개 행 조회 (Day 오름차순 정렬)
    public async Task<List<DailyRewardSlot>> GetSlotAsync(string slot)
    {
        return await _context.DailyRewardSlots
            .Where(s => s.Slot == slot)
            .OrderBy(s => s.Day)
            .ToListAsync();
    }

    // 특정 슬롯의 특정 Day 단건 조회 (복합 PK: Slot + Day)
    public async Task<DailyRewardSlot?> GetSlotDayAsync(string slot, int day)
    {
        return await _context.DailyRewardSlots
            .FirstOrDefaultAsync(s => s.Slot == slot && s.Day == day);
    }

    // 특정 슬롯의 특정 Day 보상 수정 (ExecuteUpdateAsync로 단건 UPDATE)
    public async Task UpdateSlotDayAsync(string slot, int day, int? itemId, int itemCount)
    {
        await _context.DailyRewardSlots
            .Where(s => s.Slot == slot && s.Day == day)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ItemId, itemId)
                .SetProperty(s => s.ItemCount, itemCount)
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow));
    }

    // Next 슬롯 전체를 Current 슬롯으로 복사
    // 월 전환 시 호출 — Next의 각 Day별 ItemId/ItemCount를 Current에 덮어씀
    public async Task CopyNextToCurrentAsync()
    {
        // Next 슬롯 전체 조회
        var nextRows = await _context.DailyRewardSlots
            .Where(s => s.Slot == RewardSlotKind.Next)
            .ToListAsync();

        // Current 슬롯 전체 조회 (업데이트 대상)
        var currentRows = await _context.DailyRewardSlots
            .Where(s => s.Slot == RewardSlotKind.Current)
            .ToListAsync();

        // Day별 Dictionary로 매핑하여 효율적으로 업데이트
        var nextDict = nextRows.ToDictionary(r => r.Day);
        var now = DateTime.UtcNow;

        foreach (var current in currentRows)
        {
            if (nextDict.TryGetValue(current.Day, out var next))
            {
                // Next 슬롯의 보상 값을 Current에 복사
                current.ItemId = next.ItemId;
                current.ItemCount = next.ItemCount;
                current.UpdatedAt = now;
            }
        }
    }

    // 변경 사항 DB 저장
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
