namespace Framework.Domain.ValueObjects;

// 보상 묶음 — 하나의 보상 지급에 포함될 재화·아이템 집합
// Direct 모드: Gold/Gems/Exp만 있는 단순 Currency 지급
// Mail 모드:   Items 목록을 우편으로 발송
public record RewardBundle(
    // 소프트 재화 지급량 (0이면 미지급)
    int Gold = 0,

    // 하드 재화 지급량 (0이면 미지급)
    int Gems = 0,

    // 경험치 지급량 (0이면 미지급)
    int Exp = 0,

    // 아이템 목록 (빈 리스트이면 아이템 없음)
    IReadOnlyList<RewardItem>? Items = null
)
{
    // 아이템이 없는 단순 Currency 번들인지 여부
    public bool IsCurrencyOnly => (Items is null || Items.Count == 0);

    // 지급할 내용이 하나도 없는 빈 번들 여부
    public bool IsEmpty => Gold == 0 && Gems == 0 && Exp == 0 && IsCurrencyOnly;
}
