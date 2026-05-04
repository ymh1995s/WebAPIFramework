using Framework.Api.Filters;
using Framework.Application.Features.AuditLog;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

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
        // pageSize 범위 제한 — 대용량 로그 조회는 최대 500건 허용 (M-37, D7)
        // record with expression으로 불변 record를 수정 없이 새 값 적용
        filter = filter with { PageSize = Math.Clamp(filter.PageSize, 1, 500) };

        var result = await _auditLogService.SearchAsync(filter);
        return Ok(result);
    }
}
