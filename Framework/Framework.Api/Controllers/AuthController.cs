using Framework.Application.DTOs;
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
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        try
        {
            var result = await _authService.GoogleLoginAsync(request.IdToken);
            return Ok(result);
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
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
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
