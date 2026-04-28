using Framework.Domain.Entities;

namespace Framework.Application.Features.Auth;

// 구글 계정 충돌 예외 — 동일 GoogleId가 이미 다른 플레이어에 연동된 경우 발생
// 기존 계정과 게스트 계정 정보를 함께 담아 컨트롤러에서 409 응답 구성에 활용
public class GoogleAccountConflictException : Exception
{
    // 해당 GoogleId를 이미 보유한 기존 플레이어
    public Player ExistingPlayer { get; }

    // 현재 요청을 보낸 게스트 플레이어
    public Player CurrentGuestPlayer { get; }

    public GoogleAccountConflictException(Player existingPlayer, Player currentGuestPlayer)
        : base("해당 구글 계정은 이미 다른 플레이어에 연동되어 있습니다.")
    {
        ExistingPlayer = existingPlayer;
        CurrentGuestPlayer = currentGuestPlayer;
    }
}
