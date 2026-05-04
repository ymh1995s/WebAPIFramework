using Framework.Api.Filters;
using Framework.Application.Features.Security;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 Rate Limit 로그 조회 컨트롤러 — X-Admin-Key 헤더 검증 자동 적용
// H-1 해소: AppDbContext 직접 주입 제거, ISecurityAdminService 경유로 전환
[AdminApiKey]
[ApiController]
[Route("api/admin/rate-limit-logs")]
public class AdminRateLimitLogsController : ControllerBase
{
    // 보안 통합 서비스 — Rate Limit 집계 조회 담당
    private readonly ISecurityAdminService _service;

    public AdminRateLimitLogsController(ISecurityAdminService service)
    {
        _service = service;
    }

    // IP별로 집계한 Rate Limit 초과 현황 반환 — 횟수 많은 순 정렬
    [HttpGet]
    public async Task<IActionResult> GetLogs()
    {
        // 서비스 경유 집계 조회 — typed DTO 응답 (Q3 익명 응답 → DTO 전환)
        return Ok(await _service.GetRateLimitAggregatedByIpAsync());
    }
}
