using Framework.Api.Constants;
using Framework.Api.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 서버 시간 동기화 API — 인증 없이 호출 가능, 캐시 금지
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/time")]
public class TimeController : ControllerBase
{
    // GET /api/time — 현재 서버 UTC/KST/UnixMs 반환
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        // UTC 현재 시각 1회 캡처 — 이후 파생 값 계산에 동일 기준점 사용
        var utc = DateTimeOffset.UtcNow;

        // UTC+9 오프셋으로 변환하여 KST 산출
        var kst = utc.ToOffset(TimeSpan.FromHours(9));

        // 클라이언트 정밀 시계 보정용 Unix 밀리초
        var unixMs = utc.ToUnixTimeMilliseconds();

        // 응답을 캐시하면 클라이언트가 오래된 시각을 받을 수 있으므로 no-store 강제
        Response.Headers.CacheControl = "no-store";

        return Ok(new ServerTimeResponse(utc, kst, unixMs));
    }
}
