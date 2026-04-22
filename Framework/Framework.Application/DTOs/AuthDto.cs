namespace Framework.Application.DTOs;

// 게스트 로그인 요청 DTO
public record GuestLoginRequestDto(string DeviceId);

// 토큰 응답 DTO (로그인/재발급 공통)
public record TokenResponseDto(string AccessToken, string RefreshToken, int PlayerId, bool IsNewPlayer);

// 토큰 재발급 요청 DTO
public record RefreshTokenRequestDto(string RefreshToken);
