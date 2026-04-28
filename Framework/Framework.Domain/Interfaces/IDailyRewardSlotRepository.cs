using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 일일 보상 슬롯 저장소 인터페이스
public interface IDailyRewardSlotRepository
{
    // 슬롯 전체 28개 행 조회 (slot: "Current" 또는 "Next")
    Task<List<DailyRewardSlot>> GetSlotAsync(string slot);

    // 특정 슬롯의 특정 Day 단건 조회 (없으면 null)
    Task<DailyRewardSlot?> GetSlotDayAsync(string slot, int day);

    // 특정 슬롯의 특정 Day 보상 수정 (ItemId, ItemCount 업데이트)
    Task UpdateSlotDayAsync(string slot, int day, int? itemId, int itemCount);

    // Next 슬롯 전체 내용을 Current 슬롯으로 복사
    // 월 전환 시 호출 — Next의 ItemId/ItemCount를 Current에 덮어씀
    Task CopyNextToCurrentAsync();

    // 변경 사항 DB 저장
    Task SaveChangesAsync();
}
