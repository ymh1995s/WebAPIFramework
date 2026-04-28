using Framework.Application.Features.Notice;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 공지 API — 인증 없이 접근 가능 (클라이언트가 로그인 전에도 공지를 볼 수 있어야 함)
[ApiController]
[Route("api/[controller]")]
public class NoticesController : ControllerBase
{
    private readonly INoticeService _noticeService;

    public NoticesController(INoticeService noticeService)
    {
        _noticeService = noticeService;
    }

    // GET api/notices/latest — 최신 활성 공지 1개 반환 (없으면 204)
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest()
    {
        var notice = await _noticeService.GetLatestAsync();
        return notice is null ? NoContent() : Ok(notice);
    }
}
