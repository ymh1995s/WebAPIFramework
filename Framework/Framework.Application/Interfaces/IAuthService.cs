using Framework.Application.DTOs;

// 인증 서비스 인터페이스
public interface IAuthService
{
    // 게스트 로그인 - DeviceId로 플레이어 조회 또는 신규 생성 후 토큰 발급
    Task<TokenResponseDto> GuestLoginAsync(string deviceId);

    // 리프래시 토큰으로 AccessToken 재발급
    Task<TokenResponseDto> RefreshAsync(string refreshToken);

    // 로그아웃 - 리프래시 토큰 삭제
    Task LogoutAsync(string refreshToken);

    // 구글 로그인 - IdToken 검증 후 GoogleId로 플레이어 조회 또는 신규 생성
    Task<TokenResponseDto> GoogleLoginAsync(string idToken);

    // 게스트 계정에 구글 연동 - 기존 데이터 유지하면서 GoogleId 추가
    Task LinkGoogleAsync(int playerId, string idToken);

    // 계정 탈퇴 - 플레이어 및 모든 연관 데이터 즉시 삭제
    Task WithdrawAsync(int playerId);
}
