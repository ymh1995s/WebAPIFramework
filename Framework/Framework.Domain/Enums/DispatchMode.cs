namespace Framework.Domain.Enums;

// 보상 지급 방식 — RewardDispatcher가 분기 판단에 사용
public enum DispatchMode
{
    // 즉시 지급 — PlayerProfile(Gold/Gems/Exp) 또는 PlayerItem 직접 증가
    Direct,

    // 우편 지급 — MailService를 통해 우편함으로 발송
    Mail,

    // 자동 판단 — 단일 Currency이면 Direct, 다중 아이템이면 Mail
    Auto,
}
