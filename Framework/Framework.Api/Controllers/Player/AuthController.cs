using Framework.Api.Extensions;
using Framework.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 인증 관련 API 컨트롤러
[ApiController]
[Route("auth")]
// [EnableRateLimiting]: 지정한 정책 이름의 Rate Limit을 이 컨트롤러 전체에 적용한다
// "auth" 정책 = 미인증 IP당 분당 15회 / 인증 PlayerId당 분당 30회 (ServiceExtensions.cs auth 정책 정의)
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    // 인증 관련 비즈니스 로직 처리 서비스
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // 게스트 로그인 - DeviceId로 JWT 발급
    [HttpPost("guest")]
    public async Task<IActionResult> GuestLogin([FromBody] GuestLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest("DeviceId가 필요합니다.");

        try
        {
            var result = await _authService.GuestLoginAsync(request.DeviceId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 밴된 계정 로그인 시도 — 403 반환 (인증은 됐으나 접근 거부)
            return StatusCode(403, ex.Message);
        }
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
        var currentPlayerId = User.GetPlayerId();

        try
        {
            var result = await _authService.GoogleLoginAsync(request.IdToken, currentPlayerId);
            return Ok(result);
        }
        catch (GoogleAccountConflictException ex)
        {
            // 409 Conflict — AuthService에서 미리 조립된 PlayerSummaryDto를 그대로 사용
            return Conflict(new GoogleConflictDto("GOOGLE_ACCOUNT_CONFLICT", ex.ExistingPlayer, ex.CurrentGuestPlayer));
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
        var playerId = User.GetPlayerIdRequired();

        try
        {
            await _authService.LinkGoogleAsync(playerId, request.IdToken);
            return Ok();
        }
        catch (GoogleAccountConflictException ex)
        {
            // 409 Conflict — AuthService에서 미리 조립된 PlayerSummaryDto를 그대로 사용
            return Conflict(new GoogleConflictDto("GOOGLE_ACCOUNT_CONFLICT", ex.ExistingPlayer, ex.CurrentGuestPlayer));
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
        var guestPlayerId = User.GetPlayerIdRequired();

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

    // 계정 탈퇴 - 플레이어 및 모든 연관 데이터 즉시 삭제
    [Authorize]
    [HttpDelete("withdraw")]
    public async Task<IActionResult> Withdraw()
    {
        // JWT에서 현재 로그인한 플레이어 ID 추출
        var playerId = User.GetPlayerIdRequired();
        await _authService.WithdrawAsync(playerId);
        return NoContent();
    }
}
