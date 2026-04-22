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
}
