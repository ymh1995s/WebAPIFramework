using Framework.Api.Filters;
using Framework.Application.Common;
using Framework.Application.Features.AdminNotification;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 알림 컨트롤러 — 미확인 알림 폴링, 목록 조회, 읽음 처리
[AdminApiKey]
[ApiController]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly IAdminNotificationService _service;

    public AdminNotificationsController(IAdminNotificationService service) { _service = service; }

    // GET api/admin/notifications/unread-count — 30초 폴링용
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _service.GetUnreadCountAsync();
        return Ok(new CountResponse(count));
    }

    // GET api/admin/notifications — 알림 목록
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] AdminNotificationCategory? category,
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (items, total) = await _service.SearchAsync(category, isRead, page, pageSize);
        return Ok(new AdminNotificationListResponse(items, total));
    }

    // POST api/admin/notifications/{id}/read — 단건 읽음
    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var ok = await _service.MarkReadAsync(id);
        return ok ? Ok() : NotFound();
    }

    // POST api/admin/notifications/{id}/unread — 단건 안읽음으로 되돌리기, 대상 없으면 404
    [HttpPost("{id:long}/unread")]
    public async Task<IActionResult> MarkUnread(long id)
    {
        var ok = await _service.MarkUnreadAsync(id);
        return ok ? Ok() : NotFound();
    }

    // POST api/admin/notifications/read-all — 전체 읽음
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([FromQuery] AdminNotificationCategory? category)
    {
        var count = await _service.MarkAllReadAsync(category);
        return Ok(new MarkAllReadResponse(count));
    }
}
