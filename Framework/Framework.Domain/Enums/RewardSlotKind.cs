namespace Framework.Domain.Enums;

// 일일 보상 슬롯 종류 — Current(이번 달 활성) / Next(다음 달 예약)
// DB에는 문자열 "Current" / "Next" 로 저장됨
public static class RewardSlotKind
{
    // 이번 달 활성 슬롯 — 플레이어 보상 발송에 실제로 사용되는 슬롯
    public const string Current = "Current";

    // 다음 달 예약 슬롯 — Admin이 미리 편집, 월 전환 시 Current로 복사됨
    public const string Next = "Next";
}
