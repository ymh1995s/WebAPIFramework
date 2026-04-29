using Framework.Domain.Enums;
using Framework.Domain.ValueObjects;

namespace Framework.Application.Features.Reward;

// 보상 지급 요청 DTO — RewardDispatcher 단일 진입점으로 전달
public record GrantRewardRequest(
    // 보상을 받을 플레이어 ID
    int PlayerId,

    // 보상 원천 타입 (DailyLogin, MatchComplete 등)
    RewardSourceType SourceType,

    // 멱등성 키 — SourceType 내에서 유일해야 함 (예: "2026-04-29", "match:guid")
    string SourceKey,

    // 지급할 보상 번들
    RewardBundle Bundle,

    // 우편 발송 시 사용할 제목 (Mail 모드)
    string MailTitle = "보상이 도착했습니다",

    // 우편 발송 시 사용할 본문 (Mail 모드)
    string MailBody = "보상을 수령해 주세요.",

    // 우편 만료 기간 (일, Mail 모드)
    int MailExpiresInDays = 30,

    // 지급 방식 (Auto이면 번들 구성에 따라 자동 판단)
    DispatchMode Mode = DispatchMode.Auto
);
