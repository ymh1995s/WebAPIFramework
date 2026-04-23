using Framework.Api.Filters;
using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers;

// Admin 전용 감사 로그 조회 컨트롤러 - X-Admin-Key 헤더 검증 자동 적용
[AdminApiKey]
[ApiController]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AdminAuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    // 필터/페이지네이션 조건으로 감사 로그 검색
    // 예) /api/admin/audit-logs?playerId=5&isAnomaly=true&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AuditLogFilterDto filter)
    {
        var result = await _auditLogService.SearchAsync(filter);
        return Ok(result);
    }
}
