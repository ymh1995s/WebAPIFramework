namespace Framework.Domain.Entities;

// 보상 테이블 항목 — 하나의 보상 테이블에 포함되는 아이템 행
// Weight가 null이면 고정 지급, 값이 있으면 가중치 기반 확률 지급
public class RewardTableEntry
{
    // 기본 키
    public int Id { get; set; }

    // 소속 보상 테이블 FK
    public int RewardTableId { get; set; }

    // 아이템 마스터 FK
    public int ItemId { get; set; }

    // 지급 수량
    public int Count { get; set; }

    // 가중치 (null이면 고정 지급, 값 있으면 확률 지급용 가중치)
    public int? Weight { get; set; }

    // 보상 테이블 네비게이션 프로퍼티
    public RewardTable RewardTable { get; set; } = null!;

    // 아이템 마스터 네비게이션 프로퍼티
    public Item Item { get; set; } = null!;
}
