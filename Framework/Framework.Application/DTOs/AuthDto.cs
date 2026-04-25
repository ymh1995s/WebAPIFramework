using System.ComponentModel.DataAnnotations;

namespace Framework.Application.DTOs;

// 게스트 로그인 요청 DTO
public record GuestLoginRequestDto(
    [Required]
    [MinLength(8, ErrorMessage = "DeviceId는 최소 8자 이상이어야 합니다.")]
    [MaxLength(64, ErrorMessage = "DeviceId는 64자를 초과할 수 없습니다.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "DeviceId는 영문·숫자·하이픈·언더스코어만 허용됩니다.")]
    string DeviceId
);

// 토큰 응답 DTO (로그인/재발급 공통)
public record TokenResponseDto(string AccessToken, string RefreshToken, int PlayerId, bool IsNewPlayer);

// 토큰 재발급 요청 DTO
public record RefreshTokenRequestDto(string RefreshToken);

// 구글 로그인 요청 DTO - Unity 구글 SDK가 발급한 IdToken 전달
public record GoogleLoginRequestDto(string IdToken);

// 구글 계정 연동 요청 DTO - 게스트 계정에 구글 연결
public record LinkGoogleRequestDto(string IdToken);
