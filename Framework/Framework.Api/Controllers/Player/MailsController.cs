using Framework.Api.Extensions;
using Framework.Application.Features.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 우편 API 컨트롤러 (유저 전용) - 인증된 사용자만 접근 가능
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MailsController : ControllerBase
{
    private readonly IMailService _mailService;

    public MailsController(IMailService mailService)
    {
        _mailService = mailService;
    }

    // 내 우편함 조회 - JWT에서 PlayerId 추출하여 본인 우편만 조회
    [HttpGet]
    public async Task<IActionResult> GetMyMails()
    {
        var playerId = User.GetPlayerIdRequired();
        var result = await _mailService.GetMyMailsAsync(playerId);
        return Ok(result);
    }

    // 우편 수령 - 아이템을 인벤토리로 이동 (JWT에서 추출한 본인 PlayerId로 소유자 검증)
    [HttpPost("{id}/claim")]
    public async Task<IActionResult> Claim(int id)
    {
        var playerId = User.GetPlayerIdRequired();
        var success = await _mailService.ClaimAsync(id, playerId);
        return success ? Ok() : BadRequest("이미 수령했거나 존재하지 않는 우편입니다.");
    }
}
