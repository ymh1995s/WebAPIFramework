using Framework.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 인증 관련 API 컨트롤러
[ApiController]
[Route("auth")]
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
}
