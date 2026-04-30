namespace Framework.Application.Features.Auth;

// 구글 계정 충돌 예외 — 동일 GoogleId가 이미 다른 플레이어에 연동된 경우 발생
// 컨트롤러에서 바로 409 응답을 구성할 수 있도록 PlayerSummaryDto로 미리 조립된 정보를 보유
public class GoogleAccountConflictException : Exception
{
    // 해당 GoogleId를 이미 보유한 기존 플레이어 요약 정보 (레벨 포함)
    public PlayerSummaryDto ExistingPlayer { get; }

    // 현재 요청을 보낸 게스트 플레이어 요약 정보 (레벨 포함)
    public PlayerSummaryDto CurrentGuestPlayer { get; }

    public GoogleAccountConflictException(PlayerSummaryDto existingPlayer, PlayerSummaryDto currentGuestPlayer)
        : base("해당 구글 계정은 이미 다른 플레이어에 연동되어 있습니다.")
    {
        ExistingPlayer = existingPlayer;
        CurrentGuestPlayer = currentGuestPlayer;
    }
}
