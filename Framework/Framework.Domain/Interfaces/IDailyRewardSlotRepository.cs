using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 일일 보상 슬롯 저장소 인터페이스
public interface IDailyRewardSlotRepository
{
    // 슬롯 전체 28개 행 조회 (slot: "Current" 또는 "Next")
    Task<List<DailyRewardSlot>> GetSlotAsync(string slot);

    // 특정 슬롯의 특정 Day 단건 조회 (DailyLoginService에서 보상 지급 시 사용)
    Task<DailyRewardSlot?> GetSlotDayAsync(string slot, int day);

    // 슬롯 전체 Day 보상 일괄 수정 (메모리 변경만, SaveChangesAsync는 서비스에서 호출)
    // items: (Day, ItemId, ItemCount) 튜플 컬렉션 — all-or-nothing 트랜잭션을 위해 SaveChanges 분리
    Task UpdateSlotBatchAsync(string slot, IEnumerable<(int Day, int? ItemId, int ItemCount)> items);

    // Next 슬롯 전체 내용을 Current 슬롯으로 복사
    // 월 전환 시 호출 — Next의 ItemId/ItemCount를 Current에 덮어씀
    Task CopyNextToCurrentAsync();

    // 변경 사항 DB 저장
    Task SaveChangesAsync();
}
