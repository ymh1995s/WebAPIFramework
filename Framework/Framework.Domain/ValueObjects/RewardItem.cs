namespace Framework.Domain.ValueObjects;

// 보상 번들 내 단일 아이템 단위 — 불변 값 객체
// ItemId: Item 마스터의 Id (Gold·Gems·Exp는 별도 Currency 프로퍼티로 표현)
public record RewardItem(int ItemId, int Quantity)
{
    // 수량이 1 이상이어야 유효한 보상 항목
    public bool IsValid => Quantity > 0;
}
