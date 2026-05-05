using System.ComponentModel.DataAnnotations;
using Framework.Domain.Enums;

namespace Framework.Application.Features.AdminRewardDispatch;

// 수동 보상 지급 요청 DTO
// Gold/Gems는 Items 목록에 ItemId=1(Gold) / ItemId=2(Gems)로 포함하여 전달
public record AdminGrantRewardDto(
    // 대상 플레이어 ID
    int PlayerId,

    // 멱등성 키 — AdminGrant 내에서 유일해야 함 (예: "2026-04-29-event", "support-ticket-123")
    // 영숫자와 `.` `_` `:` `-` 만 허용 — SQL 인젝션 및 구분자 충돌 방지
    [Required]
    [MaxLength(64)]
    [RegularExpression(@"^[a-zA-Z0-9._:\-]+$",
        ErrorMessage = "SourceKey는 영숫자/`.`/`_`/`:`/`-` 만 허용됩니다.")]
    string SourceKey,

    // 경험치 지급량 (미입력 시 0)
    int? Exp,

    // 지급 아이템 목록 (Gold=ItemId 1, Gems=ItemId 2, 미입력 시 없음)
    List<AdminGrantItemDto>? Items,

    // 지급 방식 (Auto=자동판단, Direct=즉시지급, Mail=우편)
    DispatchMode Mode = DispatchMode.Auto,

    // 우편 지급 시 제목 (Mail 모드) — 최대 100자
    [MaxLength(100)]
    string? MailTitle = null,

    // 우편 지급 시 본문 (Mail 모드) — 최대 2000자
    [MaxLength(2000)]
    string? MailBody = null,

    // 우편 만료 일수 (Mail 모드, 기본 30일)
    int? MailExpiresInDays = null
);

// 지급 아이템 단위 DTO
public record AdminGrantItemDto(int ItemId, int Quantity);
