using Framework.Api.Filters;
using Framework.Application.Features.Mail;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 우편 발송 컨트롤러 - X-Admin-Key 헤더 검증 자동 적용
// 단건 발송은 보상 지급 프레임워크(AdminRewardDispatchController)로 통합되었으므로 Bulk만 유지
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

    // 전체 플레이어에게 우편 일괄 발송
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkSend([FromBody] BulkSendMailDto dto)
    {
        await _mailService.BulkSendAsync(dto);
        return Created();
    }
}
