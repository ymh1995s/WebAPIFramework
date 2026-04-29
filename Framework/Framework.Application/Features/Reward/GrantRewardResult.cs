using Framework.Domain.Enums;

namespace Framework.Application.Features.Reward;

// 보상 지급 결과 DTO
public record GrantRewardResult(
    // 지급 성공 여부
    bool Success,

    // 이미 지급된 보상 (멱등성 처리 — 재요청 시 성공으로 간주)
    bool AlreadyGranted,

    // 실제 사용된 지급 방식 (Direct 또는 Mail)
    DispatchMode UsedMode,

    // 우편 지급 시 생성된 Mail ID (Direct이면 null)
    int? MailId,

    // 결과 메시지 (로깅/디버그용)
    string Message
)
{
    // 성공 — Direct 지급
    public static GrantRewardResult DirectSuccess()
        => new(true, false, DispatchMode.Direct, null, "직접 지급 완료");

    // 성공 — 우편 지급
    public static GrantRewardResult MailSuccess(int mailId)
        => new(true, false, DispatchMode.Mail, mailId, "우편 발송 완료");

    // 이미 지급된 보상
    public static GrantRewardResult Duplicate()
        => new(true, true, DispatchMode.Direct, null, "이미 지급된 보상");

    // 실패
    public static GrantRewardResult Fail(string reason)
        => new(false, false, DispatchMode.Direct, null, reason);
}
