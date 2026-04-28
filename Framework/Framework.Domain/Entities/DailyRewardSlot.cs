namespace Framework.Domain.Entities;

// 일일 보상 슬롯 엔티티 — Current/Next 2슬롯 × Day 1~28 = 총 56행 고정
// 복합 PK: (Slot, Day)
public class DailyRewardSlot
{
    // 슬롯 종류 — "Current"(이번 달) 또는 "Next"(다음 달 예약)
    public string Slot { get; set; } = string.Empty;

    // 사이클 일차 — 1~28 범위 고정
    public int Day { get; set; }

    // 해당 일차의 보상 아이템 ID (null이면 보상 없음)
    public int? ItemId { get; set; }

    // 보상 아이템 수량 (ItemId가 null일 경우 0)
    public int ItemCount { get; set; }

    // 마지막 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; }

    // 내비게이션 프로퍼티 — Items 테이블 참조 (nullable)
    public Item? Item { get; set; }
}
