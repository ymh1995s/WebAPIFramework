using Framework.Api.Filters;
using Framework.Application.Features.Mail;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 우편 발송 컨트롤러 - X-Admin-Key 헤더 검증 자동 적용
[AdminApiKey]
[ApiController]
[Route("api/admin/mails")]
public class AdminMailsController : ControllerBase
{
    private readonly IMailService _mailService;

    public AdminMailsController(IMailService mailService)
    {
        _mailService = mailService;
    }

    // 단일 플레이어에게 우편 발송
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMailDto dto)
    {
        await _mailService.SendAsync(dto);
        return Created();
    }

    // 전체 플레이어에게 우편 일괄 발송
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkSend([FromBody] BulkSendMailDto dto)
    {
        await _mailService.BulkSendAsync(dto);
        return Created();
    }
}
