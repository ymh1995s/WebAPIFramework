using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// 우편 API 컨트롤러
[ApiController]
[Route("api/[controller]")]
public class MailsController : ControllerBase
{
    private readonly IMailService _mailService;

    public MailsController(IMailService mailService)
    {
        _mailService = mailService;
    }

    // 단일 플레이어에게 보상 발송
    [HttpPost]
    public async Task<IActionResult> Send(SendMailDto dto)
    {
        await _mailService.SendAsync(dto);
        return Created();
    }

    // 전체 플레이어에게 보상 일괄 발송
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkSend(BulkSendMailDto dto)
    {
        await _mailService.BulkSendAsync(dto);
        return Created();
    }

    // 우편 수령
    [HttpPost("{id}/claim")]
    public async Task<IActionResult> Claim(int id)
    {
        var success = await _mailService.ClaimAsync(id);
        return success ? Ok() : BadRequest("이미 수령했거나 존재하지 않는 우편입니다.");
    }
}
