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

    // 특정 슬롯의 특정 Day 단건 조회 (DailyLoginService에서 보상 지급 시 사용)
    public async Task<DailyRewardSlot?> GetSlotDayAsync(string slot, int day)
    {
        return await _context.DailyRewardSlots
            .FirstOrDefaultAsync(s => s.Slot == slot && s.Day == day);
    }

    // 슬롯 전체 Day 보상 일괄 수정
    // items에 포함된 Day의 행만 ItemId, ItemCount, UpdatedAt을 갱신하고 메모리에 반영
    // SaveChangesAsync는 서비스에서 별도로 호출 (all-or-nothing 트랜잭션 보장)
    public async Task UpdateSlotBatchAsync(string slot, IEnumerable<(int Day, int? ItemId, int ItemCount)> items)
    {
        // 해당 슬롯 전체 행 로드 (변경 추적 대상)
        var rows = await _context.DailyRewardSlots
            .Where(s => s.Slot == slot)
            .ToListAsync();

        // Day → 엔티티 매핑 (빠른 조회)
        var rowDict = rows.ToDictionary(r => r.Day);
        var now = DateTime.UtcNow;

        foreach (var (day, itemId, itemCount) in items)
        {
            if (!rowDict.TryGetValue(day, out var row))
                continue; // 존재하지 않는 Day는 건너뜀 (서비스에서 이미 검증)

            // 메모리 상 엔티티 값 갱신 — EF Core 변경 추적이 감지
            row.ItemId = itemId;
            row.ItemCount = itemCount;
            row.UpdatedAt = now;
        }
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
