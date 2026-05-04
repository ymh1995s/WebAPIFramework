using System.Text;
using System.Text.Json;
using Framework.Application.Features.Iap;
using Framework.Api.Services.IapStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// Google Play RTDN(Real-time Developer Notifications) 수신 컨트롤러
// [중요] AllowAnonymous — Google Pub/Sub 서버가 직접 호출, JWT 인증 불가
// [중요] 모든 응답은 반드시 200 OK — Pub/Sub은 비-200 응답 시 최대 7일간 재시도
[AllowAnonymous]
[ApiController]
[Route("api/iap/google")]
[EnableRateLimiting("iap-rtdn")]
public class IapRtdnController : ControllerBase
{
    private readonly IIapRtdnService _rtdnService;
    private readonly GooglePubSubAuthenticator _authenticator;
    private readonly IConfiguration _config;
    private readonly ILogger<IapRtdnController> _logger;

    public IapRtdnController(
        IIapRtdnService rtdnService,
        GooglePubSubAuthenticator authenticator,
        IConfiguration config,
        ILogger<IapRtdnController> logger)
    {
        _rtdnService = rtdnService;
        _authenticator = authenticator;
        _config = config;
        _logger = logger;
    }

    // POST /api/iap/google/rtdn
    // Google Pub/Sub 푸시 알림 수신 엔드포인트
    // 처리 순서: OIDC 검증 → base64 디코딩 → PackageName 검증 → 서비스 처리
    [HttpPost("rtdn")]
    public async Task<IActionResult> ReceiveRtdn([FromBody] PubSubPushRequest request)
    {
        try
        {
            // 1단계: OIDC 토큰 검증 — Google Pub/Sub 서버가 보낸 요청인지 확인
            var authHeader = HttpContext.Request.Headers.Authorization.ToString();
            var isValid = await _authenticator.ValidateAsync(authHeader);

            if (!isValid)
            {
                // [중요] 검증 실패에도 200 반환 — Pub/Sub 재시도 루프 방지
                // 악의적 위조 요청이 재시도될 경우 서버 부하 유발 위험이 있으므로
                // 내부적으로는 경고 로그를 남겨 보안 감시가 가능하도록 함
                _logger.LogWarning(
                    "RTDN OIDC 검증 실패 — IP: {Ip}, Subscription: {Sub}",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    request.Subscription);
                return Ok(new { ok = false, reason = "auth_failed" });
            }

            // 2단계: Pub/Sub 메시지의 Data 필드를 base64 디코딩 → UTF-8 문자열
            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(request.Message.Data);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "RTDN base64 디코딩 실패 — MessageId: {MsgId}", request.Message.MessageId);
                return Ok(new { ok = false, reason = "invalid_base64" });
            }

            var json = Encoding.UTF8.GetString(decodedBytes);

            // 3단계: JSON 역직렬화 — camelCase 키 대소문자 무시
            RtdnPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RtdnPayload>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "RTDN JSON 역직렬화 실패 — MessageId: {MsgId}", request.Message.MessageId);
                return Ok(new { ok = false, reason = "invalid_json" });
            }

            if (payload is null)
            {
                _logger.LogWarning("RTDN 페이로드 null — MessageId: {MsgId}", request.Message.MessageId);
                return Ok(new { ok = false, reason = "null_payload" });
            }

            // 4단계: PackageName 검증 — 다른 앱의 알림이 이 서버로 잘못 라우팅되는 경우 차단
            var expectedPackage = _config["Iap:Google:PackageName"];
            if (!string.IsNullOrWhiteSpace(expectedPackage) &&
                !string.Equals(payload.PackageName, expectedPackage, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "RTDN PackageName 불일치 — 수신: {Received}, 기대: {Expected}",
                    payload.PackageName, expectedPackage);
                return Ok(new { ok = false, reason = "package_mismatch" });
            }

            // 5단계: 서비스 계층에 위임하여 알림 유형별 처리 수행
            await _rtdnService.HandleAsync(payload);

            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            // [D4 의도] Google Pub/Sub 재시도 루프 차단
            // 비-200 응답 시 Pub/Sub은 최대 7일간 재시도 폭주를 유발한다.
            // 도메인 예외·예기치 않은 오류 모두 200으로 응답하여 Pub/Sub은 정상 종료시키고,
            // 운영자는 LogError로 남겨진 로그 또는 AdminNotification으로 오류를 인지한다.
            _logger.LogError(
                ex,
                "RTDN 처리 실패 — 200으로 응답하여 Pub/Sub 재시도 차단. MessageId: {MsgId}",
                request?.Message?.MessageId ?? "unknown");
            return Ok(new { ok = false, reason = "internal_error" });
        }
    }
}
