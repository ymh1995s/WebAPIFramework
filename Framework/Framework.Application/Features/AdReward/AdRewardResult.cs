namespace Framework.Application.Features.AdReward;

// 광고 보상 처리 결과 — AdRewardService.ProcessCallbackAsync 반환값
public record AdRewardResult(
    // 보상 지급 성공 여부
    bool Success,

    // 이미 지급된 보상 (멱등성 처리 — 동일 트랜잭션 재콜백 시)
    bool AlreadyGranted,

    // 결과 메시지 (로깅/디버그용)
    string Message,

    // 보상을 받은 플레이어 ID (실패 시 null)
    int? PlayerId
)
{
    // 지급 성공
    public static AdRewardResult Ok(int playerId)
        => new(true, false, "광고 보상 지급 완료", playerId);

    // 이미 지급된 보상 (중복 콜백)
    public static AdRewardResult Duplicate(int playerId)
        => new(true, true, "이미 지급된 보상", playerId);

    // 실패
    public static AdRewardResult Fail(string reason)
        => new(false, false, reason, null);
}
