using Framework.Api.Constants;
using Framework.Api.ProblemDetails;
using Framework.Application.Features.AdReward;
using Framework.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 광고 SSV(Server Side Verification) 콜백 컨트롤러
// [중요] AllowAnonymous — 광고 네트워크 서버가 직접 호출하므로 JWT 인증 불가
// PlayerId는 콜백 서명 검증 이후 파라미터에서 추출 (User.FindFirst 사용 금지)
[AllowAnonymous]
[ApiController]
[Route("api/ads/callback")]
[EnableRateLimiting(RateLimitPolicies.AdsCallback)]
public class AdsCallbackController : ControllerBase
{
    private readonly IAdRewardService _adRewardService;
    private readonly ILogger<AdsCallbackController> _logger;

    public AdsCallbackController(
        IAdRewardService adRewardService,
        ILogger<AdsCallbackController> logger)
    {
        _adRewardService = adRewardService;
        _logger = logger;
    }

    // Unity Ads SSV 콜백 — 광고 시청 완료 후 Unity Ads 서버가 직접 호출
    [HttpGet("unity-ads")]
    public async Task<IActionResult> UnityAdsCallback()
    {
        return await HandleCallback(AdNetworkType.UnityAds);
    }

    // IronSource SSV 콜백 — 광고 시청 완료 후 IronSource 서버가 직접 호출
    [HttpGet("ironsource")]
    public async Task<IActionResult> IronSourceCallback()
    {
        return await HandleCallback(AdNetworkType.IronSource);
    }

    // 광고 네트워크 공통 콜백 처리 로직
    private async Task<IActionResult> HandleCallback(AdNetworkType network)
    {
        // 요청 IP 추출 (로깅/보안 감시 목적)
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // 쿼리 파라미터를 Dictionary로 변환 (검증기에 전달)
        var queryParams = HttpContext.Request.Query
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        // 원본 쿼리 스트링 (HMAC 서명 검증에 사용)
        var rawQuery = HttpContext.Request.QueryString.Value ?? string.Empty;

        var ctx = new AdCallbackContext(
            QueryParams: queryParams,
            RawQueryString: rawQuery,
            RemoteIp: remoteIp
        );

        try
        {
            var result = await _adRewardService.ProcessCallbackAsync(network, ctx);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "광고 보상 처리 실패 — Network: {Network}, IP: {Ip}, 이유: {Message}",
                    network, remoteIp, result.Message);
                // 광고 네트워크에 200을 반환해야 재시도 루프가 발생하지 않음
                // 실패 사유는 내부 로그로만 처리
                return Ok(new { ok = false, message = result.Message });
            }

            return Ok(new { ok = true, alreadyGranted = result.AlreadyGranted });
        }
        catch (InvalidAdSignatureException ex)
        {
            _logger.LogWarning(ex, "광고 서명 검증 실패 — Network: {Network}, IP: {Ip}", network, remoteIp);
            // 401 반환 — 광고 네트워크가 서명 오류를 인지할 수 있도록 비-200 허용 (D2 정책 예외)
            return Unauthorized(new { ok = false, errorCode = ErrorCodes.AdSignatureInvalid, message = "서명 검증 실패" });
        }
        catch (AdPolicyNotFoundException ex)
        {
            _logger.LogWarning(ex, "광고 정책 없음 — Network: {Network}", network);
            // [D2] 광고 네트워크 재시도 회피 — 200으로 응답, errorCode로 클라이언트 분기 가능
            return Ok(new { ok = false, errorCode = ErrorCodes.AdPolicyNotFound, message = "등록된 광고 정책이 없습니다." });
        }
        catch (AdDailyLimitExceededException ex)
        {
            _logger.LogInformation(ex, "일일 한도 초과 — Network: {Network}", network);
            // [D2] 광고 네트워크 재시도 회피 — 200으로 응답, errorCode로 클라이언트 분기 가능
            return Ok(new { ok = false, errorCode = ErrorCodes.AdDailyLimitExceeded, message = "오늘의 광고 보상 한도에 도달했습니다." });
        }
        catch (Exception ex)
        {
            // [D2] catch-all 유지 — 광고 네트워크 재시도 회피 목적, 200으로 응답
            _logger.LogError(ex, "광고 콜백 처리 중 오류 — Network: {Network}, IP: {Ip}", network, remoteIp);
            return Ok(new { ok = false, errorCode = ErrorCodes.InternalError, message = "서버 내부 오류" });
        }
    }
}
