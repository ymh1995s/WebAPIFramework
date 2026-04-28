using System.ComponentModel.DataAnnotations;

namespace Framework.Application.Features.Auth;

// 게스트 로그인 요청 DTO
public record GuestLoginRequestDto(
    [Required]
    [MinLength(8, ErrorMessage = "DeviceId는 최소 8자 이상이어야 합니다.")]
    [MaxLength(64, ErrorMessage = "DeviceId는 64자를 초과할 수 없습니다.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "DeviceId는 영문·숫자·하이픈·언더스코어만 허용됩니다.")]
    string DeviceId
);

// 토큰 응답 DTO (로그인/재발급 공통) — PlayerId는 외부 공개용 Guid (내부 정수 Id 은닉)
public record TokenResponseDto(string AccessToken, string RefreshToken, Guid PlayerId, bool IsNewPlayer);

// 토큰 재발급 요청 DTO
public record RefreshTokenRequestDto(string RefreshToken);

// 구글 로그인 요청 DTO - Unity 구글 SDK가 발급한 IdToken 전달
public record GoogleLoginRequestDto(string IdToken);

// 구글 계정 연동 요청 DTO - 게스트 계정에 구글 연결
public record LinkGoogleRequestDto(string IdToken);

// 계정 충돌 응답 DTO — 구글 로그인/연동 시 GoogleId가 이미 다른 계정에 연동된 경우 반환
public record GoogleConflictDto(
    // 에러 코드 — 클라이언트가 충돌 상황을 식별하기 위한 상수
    string ErrorCode,
    // 해당 GoogleId를 이미 보유한 기존 계정
    PlayerSummaryDto ExistingPlayer,
    // 현재 요청을 보낸 게스트 계정
    PlayerSummaryDto CurrentGuestPlayer
);

// 플레이어 요약 정보 DTO — 충돌 화면에서 두 계정 비교 표시용 (재화 정보 미포함)
// PlayerId는 외부 공개용 Guid — 내부 정수 Id 노출 금지
public record PlayerSummaryDto(
    Guid PlayerId,
    string Nickname,
    int Level,
    DateTime CreatedAt,
    DateTime LastLoginAt
);

// 구글 계정 충돌 해소 요청 DTO — 게스트 계정을 포기하고 기존 계정으로 전환
public record ResolveGoogleConflictRequestDto(string IdToken);
