using Framework.Api.Filters;
using Framework.Application.Features.Iap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Controllers.Player;

// 인앱결제 구매 검증 컨트롤러 — 플레이어 전용
// [Authorize]: JWT 인증 필수
// [RequireLinkedAccount]: Google 계정 연동 필수 — 게스트 계정 결제 차단
[Authorize]
[RequireLinkedAccount]
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
        // JWT 클레임에서 PlayerId 추출
        var playerIdClaim = User.FindFirst("playerId");
        if (playerIdClaim is null || !int.TryParse(playerIdClaim.Value, out var playerId))
            return Unauthorized(new { message = "유효하지 않은 인증 정보입니다." });

        // 클라이언트 IP 추출 (어뷰징 탐지용)
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var result = await _purchaseService.VerifyAndGrantAsync(playerId, request, clientIp);
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
            return NotFound(new { message = ex.Message });
        }
        catch (IapReceiptInvalidException ex)
        {
            // 영수증 위변조 또는 상태 이상 — 409 Conflict 반환
            _logger.LogWarning(
                "IAP 영수증 검증 실패 — PlayerId: {PlayerId}, 사유: {Reason}",
                playerId, ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (IapTokenOwnershipMismatchException ex)
        {
            // 다른 플레이어의 토큰으로 요청 — 422 Unprocessable Entity 반환
            _logger.LogWarning(
                "IAP 토큰 소유자 불일치 — PlayerId: {PlayerId}",
                playerId);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (IapVerifierException ex)
        {
            // 외부 스토어 API 오류 — 서버 측 문제이므로 500 반환
            _logger.LogError(
                ex,
                "IAP 스토어 API 오류 — PlayerId: {PlayerId}, ProductId: {ProductId}",
                playerId, request.ProductId);
            return StatusCode(500, new { message = "스토어 검증 서버와 통신 중 오류가 발생했습니다. 잠시 후 다시 시도해주세요." });
        }
        catch (Exception ex)
        {
            // 예기치 않은 오류 — 500 반환
            _logger.LogError(
                ex,
                "IAP 처리 중 예기치 않은 오류 — PlayerId: {PlayerId}",
                playerId);
            return StatusCode(500, new { message = "서버 내부 오류가 발생했습니다." });
        }
    }
}
