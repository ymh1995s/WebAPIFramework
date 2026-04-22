using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 매치메이킹 API 컨트롤러 - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MatchMakingController : ControllerBase
{
    private readonly IMatchMakingService _matchMakingService;

    public MatchMakingController(IMatchMakingService matchMakingService)
    {
        _matchMakingService = matchMakingService;
    }

    // POST api/matchmaking - 매칭 참가 요청
    [HttpPost]
    public async Task<IActionResult> Join([FromBody] JoinMatchRequestDto request)
    {
        var result = await _matchMakingService.JoinAsync(request);

        if (result.IsDuplicate) return Conflict(result);
        return Ok(result);
    }

    // DELETE api/matchmaking/{userId} - 매칭 취소 요청
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Cancel(string userId)
    {
        var result = await _matchMakingService.CancelAsync(userId);

        if (result is null)
            return NotFound(new { Message = $"{userId} 는 대기열에 없습니다." });

        return Ok(result);
    }
}
