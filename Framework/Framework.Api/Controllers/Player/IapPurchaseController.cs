using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Api.Filters;
using Framework.Api.ProblemDetails;
using Framework.Application.Features.Iap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 인앱결제 구매 검증 컨트롤러 — 플레이어 전용
// [Authorize]: JWT 인증 필수
// [RequireLinkedAccount]: Google 계정 연동 필수 — 게스트 계정 결제 차단
[Authorize]
[RequireLinkedAccount]
[EnableRateLimiting(RateLimitPolicies.IapVerify)]
[ApiController]
[Route("api/iap")]
public class IapPurchaseController : ControllerBase
{
    private readonly IIapPurchaseService _purchaseService;
    private readonly ILogger<IapPurchaseController> _logger;

    public IapPurchaseController(
        IIapPurchaseService purchaseService,
        ILogger<IapPurchaseController> logger)
    {
        _purchaseService = purchaseService;
        _logger = logger;
    }

    // Google Play 구매 영수증 검증 및 보상 지급
    // 클라이언트는 Google Play에서 발급받은 purchaseToken을 이 API로 전송
    // 서버에서 Google API로 검증 후 보상 지급
    [HttpPost("google/verify")]
    public async Task<IActionResult> VerifyGooglePurchase([FromBody] IapVerifyRequest request)
    {
        // JWT 클레임에서 PlayerId 추출 — 안전 버전: 클레임 없으면 null 반환
        var playerId = User.GetPlayerId();
        if (playerId is null)
            return Unauthorized(new { message = "유효하지 않은 인증 정보입니다." });

        // 클라이언트 IP 추출 (어뷰징 탐지용)
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var result = await _purchaseService.VerifyAndGrantAsync(playerId.Value, request, clientIp);
            return Ok(new
            {
                ok = result.Ok,
                alreadyGranted = result.AlreadyGranted,
                purchaseId = result.PurchaseId
            });
        }
        catch (IapProductNotFoundException ex)
        {
            // 등록되지 않은 상품 ID — 404 반환
            _logger.LogWarning(
                "IAP 상품 없음 — PlayerId: {PlayerId}, ProductId: {ProductId}",
                playerId, request.ProductId);
            return Problem(
                title: "IAP 상품 미존재",
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                type: "https://framework.api/errors/iap-product-not-found",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.IapProductNotFound });
        }
        catch (IapReceiptInvalidException ex)
        {
            // 영수증 위변조 또는 상태 이상 — 409 Conflict 반환
            _logger.LogWarning(
                "IAP 영수증 검증 실패 — PlayerId: {PlayerId}, 사유: {Reason}",
                playerId, ex.Message);
            return Problem(
                title: "IAP 영수증 검증 실패",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                type: "https://framework.api/errors/iap-receipt-invalid",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.IapReceiptInvalid });
        }
        catch (IapTokenOwnershipMismatchException ex)
        {
            // 다른 플레이어의 토큰으로 요청 — 422 Unprocessable Entity 반환
            _logger.LogWarning(
                "IAP 토큰 소유자 불일치 — PlayerId: {PlayerId}",
                playerId);
            return Problem(
                title: "IAP 토큰 소유자 불일치",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                type: "https://framework.api/errors/iap-token-ownership-mismatch",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.IapTokenOwnershipMismatch });
        }
        catch (IapVerifierException ex)
        {
            // 외부 Google Play API 오류 — 의존 서비스 장애이므로 502 BadGateway 반환 (M-22)
            // Unity 클라이언트 미작성 시점이므로 호환성 부담 없음
            _logger.LogError(
                ex,
                "IAP 스토어 API 오류 — PlayerId: {PlayerId}, ProductId: {ProductId}",
                playerId, request.ProductId);
            return Problem(
                title: "외부 스토어 검증 서비스 오류",
                detail: "스토어 검증 서버와 통신 중 오류가 발생했습니다. 잠시 후 다시 시도해주세요.",
                statusCode: StatusCodes.Status502BadGateway,
                type: "https://framework.api/errors/iap-verifier-error",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.IapVerifierError });
        }
        // catch-all 제거 — 일반 Exception은 GlobalExceptionHandler에 위임 (M-22)
    }
}
