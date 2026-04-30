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
    // PlayerId 존재 여부 확인을 위한 플레이어 저장소
    private readonly IPlayerRepository _playerRepository;

    public AdminMailsController(IMailService mailService, IPlayerRepository playerRepository)
    {
        _mailService = mailService;
        _playerRepository = playerRepository;
    }

    // 단일 플레이어에게 우편 발송
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMailDto dto)
    {
        // PlayerId 존재 여부 확인 — FK 위반 방지
        var player = await _playerRepository.GetByIdAsync(dto.PlayerId);
        if (player is null)
            return NotFound(new { message = "플레이어를 찾을 수 없습니다." });

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
