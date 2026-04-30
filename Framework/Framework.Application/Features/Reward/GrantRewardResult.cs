using Framework.Domain.Enums;

namespace Framework.Application.Features.Reward;

// 보상 지급 결과 DTO
public record GrantRewardResult
{
    // 지급 성공 여부
    public bool Success { get; init; }

    // 이미 지급된 보상 (멱등성 처리 — 재요청 시 성공으로 간주)
    public bool AlreadyGranted { get; init; }

    // 플레이어 미존재 여부
    public bool IsNotFound { get; init; }

    // 실제 사용된 지급 방식 (Direct 또는 Mail)
    public DispatchMode UsedMode { get; init; }

    // 우편 지급 시 생성된 Mail ID (Direct이면 null)
    public int? MailId { get; init; }

    // 결과 메시지 (로깅/디버그용)
    public string Message { get; init; } = "";

    // 성공 — Direct 지급
    public static GrantRewardResult DirectSuccess() => new()
    {
        Success = true,
        AlreadyGranted = false,
        UsedMode = DispatchMode.Direct,
        MailId = null,
        Message = "직접 지급 완료"
    };

    // 성공 — 우편 지급
    public static GrantRewardResult MailSuccess(int mailId) => new()
    {
        Success = true,
        AlreadyGranted = false,
        UsedMode = DispatchMode.Mail,
        MailId = mailId,
        Message = "우편 발송 완료"
    };

    // 이미 지급된 보상
    public static GrantRewardResult Duplicate() => new()
    {
        Success = true,
        AlreadyGranted = true,
        UsedMode = DispatchMode.Direct,
        MailId = null,
        Message = "이미 지급된 보상"
    };

    // 플레이어 미존재 — FK 위반 방지용 사전 검증 실패
    public static GrantRewardResult NotFound() => new()
    {
        Success = false,
        IsNotFound = true,
        Message = "플레이어를 찾을 수 없습니다."
    };

    // 실패
    public static GrantRewardResult Fail(string reason) => new()
    {
        Success = false,
        UsedMode = DispatchMode.Direct,
        Message = reason
    };
}
