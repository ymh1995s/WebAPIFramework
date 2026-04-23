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
    // 클라이언트가 보낸 UserId 필드는 무시하고 JWT의 PlayerId로 덮어씀 — 타 유저 사칭 방지
    [HttpPost]
    public async Task<IActionResult> Join([FromBody] JoinMatchRequestDto request)
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var safeRequest = request with { UserId = playerId.ToString() };

        var result = await _matchMakingService.JoinAsync(safeRequest);

        if (result.IsDuplicate) return Conflict(result);
        return Ok(result);
    }

    // DELETE api/matchmaking - 본인 매칭 취소 (JWT의 PlayerId 기준)
    [HttpDelete]
    public async Task<IActionResult> Cancel()
    {
        var playerId = int.Parse(User.FindFirst("playerId")!.Value);
        var result = await _matchMakingService.CancelAsync(playerId.ToString());

        if (result is null)
            return NotFound(new { Message = "대기열에 없습니다." });

        return Ok(result);
    }
}
