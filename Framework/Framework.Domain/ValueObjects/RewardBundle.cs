namespace Framework.Domain.ValueObjects;

// 보상 묶음 — 하나의 보상 지급에 포함될 재화·아이템 집합
// Direct 모드: Exp만 있는 단순 지급 또는 Items(통화 포함) 즉시 지급
// Mail 모드:   Items 목록을 우편으로 발송
// [Currency-as-Item] Gold(ItemId=1), Gems(ItemId=2)는 Items 목록에 포함하여 전달 — 별도 필드 없음
public record RewardBundle(
    // 경험치 지급량 (0이면 미지급)
    int Exp = 0,

    // 아이템 목록 — Gold(ItemId=1), Gems(ItemId=2) 통화 아이템 포함 (빈 리스트이면 아이템 없음)
    IReadOnlyList<RewardItem>? Items = null
)
{
    // 아이템이 없는 단순 번들인지 여부 (Exp만 있거나 완전 빈 번들)
    public bool IsCurrencyOnly => (Items is null || Items.Count == 0);

    // 지급할 내용이 하나도 없는 빈 번들 여부
    public bool IsEmpty => Exp == 0 && IsCurrencyOnly;
}
