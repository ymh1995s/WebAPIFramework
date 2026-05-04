using Framework.Api.Filters;
using Framework.Application.Features.Security;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Admin;

// Admin 전용 보안 통합 타임라인 컨트롤러 — X-Admin-Key 헤더 검증 자동 적용
// H-1 해소: AppDbContext 직접 주입 제거, ISecurityAdminService 경유로 전환
[AdminApiKey]
[ApiController]
[Route("api/admin/security")]
public class AdminSecurityController : ControllerBase
{
    // 보안 통합 서비스 — 타임라인 조합 담당
    private readonly ISecurityAdminService _service;

    // Rate Limit 정책 설정값 조회용 — GetRateLimitConfig에서만 사용 (서비스 경유 안 함, Q2 결정)
    private readonly IConfiguration _config;

    public AdminSecurityController(ISecurityAdminService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    // Rate Limit 정책 현재 설정값 반환 — Admin 페이지 동적 표시용
    // auth 정책은 인증 여부에 따라 한도가 분기되므로 두 값을 모두 반환
    // IConfiguration 직접 사용 유지 (Q2 결정: Service 경유 안 함)
    [HttpGet("rate-limit-config")]
    public IActionResult GetRateLimitConfig()
    {
        return Ok(new RateLimitConfigDto(
            // 미인증(IP) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPermitLimit 키
            AuthPermitLimit: _config.GetValue<int>("RateLimiting:AuthPermitLimit", 15),
            // 인증(PlayerId) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPlayerPermitLimit 키
            AuthPlayerPermitLimit: _config.GetValue<int>("RateLimiting:AuthPlayerPermitLimit", 30)
        ));
    }

    // 보안 이벤트 통합 타임라인 조회
    // Rate Limit 초과 / AuditLog 이상치 / 밴 처리 플레이어를 하나의 타임라인으로 반환
    // 본문 로직은 ISecurityAdminService.GetTimelineAsync로 이전 (H-1 해소)
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? playerId,
        [FromQuery] string? ip)
    {
        // UTC 변환은 컨트롤러-서비스 경계에서 처리 후 전달
        var fromUtc = from?.ToUniversalTime();
        var toUtc   = to?.ToUniversalTime();

        return Ok(await _service.GetTimelineAsync(fromUtc, toUtc, playerId, ip));
    }
}

// Rate Limit 정책 설정 응답 DTO — appsettings.json 의 RateLimiting 섹션에서 동적으로 읽어 반환
// AdminSecurityController 전용 (IConfiguration 직접 사용 패턴 유지, Q2 결정)
public record RateLimitConfigDto(
    // 미인증(IP) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPermitLimit 키
    int AuthPermitLimit,
    // 인증(PlayerId) 기준 분당 허용 횟수 — appsettings.json 의 RateLimiting:AuthPlayerPermitLimit 키
    int AuthPlayerPermitLimit
);
