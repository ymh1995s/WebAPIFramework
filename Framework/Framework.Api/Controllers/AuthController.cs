using Framework.Application.DTOs;
using Framework.Application.Exceptions;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers;

// 인증 관련 API 컨트롤러
[ApiController]
[Route("auth")]
// [EnableRateLimiting]: 지정한 정책 이름의 Rate Limit을 이 컨트롤러 전체에 적용한다
// "auth" 정책 = 동일 IP 기준 1분에 10회 초과 시 429 반환 (ServiceExtensions에서 정의)
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    // 충돌 응답 구성 시 두 계정의 레벨 정보를 조회하기 위해 주입
    private readonly IPlayerProfileRepository _profileRepo;

    public AuthController(IAuthService authService, IPlayerProfileRepository profileRepo)
    {
        _authService = authService;
        _profileRepo = profileRepo;
    }

    // 게스트 로그인 - DeviceId로 JWT 발급
    [HttpPost("guest")]
    public async Task<IActionResult> GuestLogin([FromBody] GuestLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest("DeviceId가 필요합니다.");

        var result = await _authService.GuestLoginAsync(request.DeviceId);
        return Ok(result);
    }

    // AccessToken 재발급
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    // 로그아웃 - 리프래시 토큰 삭제
    // [Authorize]: 요청 헤더의 Bearer 토큰을 자동 검증 - 토큰 없거나 유효하지 않으면 401 반환, 통과 시에만 메서드 실행
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    // 구글 로그인 - Unity 구글 SDK가 발급한 IdToken으로 JWT 발급
    // JWT가 있으면 게스트 플레이어 ID를 읽어 충돌 감지에 활용, 없으면 비인증 요청으로 처리
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        // JWT 클레임에서 playerId 추출 시도 — 토큰이 없거나 클레임이 없으면 null
        int? currentPlayerId = null;
        var playerIdClaim = User.FindFirst("playerId");
        if (playerIdClaim is not null && int.TryParse(playerIdClaim.Value, out var pid))
            currentPlayerId = pid;

        try
        {
            var result = await _authService.GoogleLoginAsync(request.IdToken, currentPlayerId);
            return Ok(result);
        }
        catch (GoogleAccountConflictException ex)
        {
            // 409 Conflict — 클라이언트는 충돌 해소 화면을 띄워야 함
            var conflict = await BuildConflictDtoAsync(ex);
            return Conflict(conflict);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    // 게스트 계정에 구글 연동 - 기존 데이터 유지하면서 GoogleId 추가
    [Authorize]
    [HttpPost("link/google")]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleRequestDto request)
    {
        // JWT에서 현재 로그인한 플레이어 ID 추출
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);

        try
        {
            await _authService.LinkGoogleAsync(playerId, request.IdToken);
            return Ok();
        }
        catch (GoogleAccountConflictException ex)
        {
            // 409 Conflict — 충돌 응답을 GoogleConflictDto로 반환
            var conflict = await BuildConflictDtoAsync(ex);
            return Conflict(conflict);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // 구글 계정 충돌 해소 — 게스트 계정을 소프트 딜리트하고 구글 연동 계정으로 토큰 발급
    [Authorize]
    [HttpPost("google/resolve-conflict")]
    public async Task<IActionResult> ResolveGoogleConflict([FromBody] ResolveGoogleConflictRequestDto request)
    {
        // JWT에서 현재 로그인한 게스트 플레이어 ID 추출
        var guestPlayerId = int.Parse(User.FindFirst("playerId")!.Value);

        try
        {
            var result = await _authService.ResolveGoogleConflictAsync(guestPlayerId, request.IdToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // 충돌 예외에서 GoogleConflictDto 생성 — 양쪽 계정의 레벨 정보를 포함
    private async Task<GoogleConflictDto> BuildConflictDtoAsync(GoogleAccountConflictException ex)
    {
        // PlayerProfile은 AuthService에서 접근하기 어려우므로 컨트롤러에서 별도 조회
        // (PlayerProfile 리포지토리를 AuthController에 주입하여 Level 획득)
        var existingProfile = await _profileRepo.GetByPlayerIdAsync(ex.ExistingPlayer.Id);
        var guestProfile = await _profileRepo.GetByPlayerIdAsync(ex.CurrentGuestPlayer.Id);

        // PlayerId 자리에 외부 공개용 PublicId(Guid)를 사용 — 내부 정수 Id 노출 금지
        return new GoogleConflictDto(
            ErrorCode: "GOOGLE_ACCOUNT_CONFLICT",
            ExistingPlayer: new PlayerSummaryDto(
                ex.ExistingPlayer.PublicId,
                ex.ExistingPlayer.Nickname,
                existingProfile?.Level ?? 1,
                ex.ExistingPlayer.CreatedAt,
                ex.ExistingPlayer.LastLoginAt
            ),
            CurrentGuestPlayer: new PlayerSummaryDto(
                ex.CurrentGuestPlayer.PublicId,
                ex.CurrentGuestPlayer.Nickname,
                guestProfile?.Level ?? 1,
                ex.CurrentGuestPlayer.CreatedAt,
                ex.CurrentGuestPlayer.LastLoginAt
            )
        );
    }

    // 계정 탈퇴 - 플레이어 및 모든 연관 데이터 즉시 삭제
    [Authorize]
    [HttpDelete("withdraw")]
    public async Task<IActionResult> Withdraw()
    {
        // JWT에서 현재 로그인한 플레이어 ID 추출
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        await _authService.WithdrawAsync(playerId);
        return NoContent();
    }
}
