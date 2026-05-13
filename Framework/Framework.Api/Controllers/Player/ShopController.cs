using Framework.Api.Constants;
using Framework.Api.Extensions;
using Framework.Api.ProblemDetails;
using Framework.Api.Requests;
using Framework.Application.Features.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Framework.Api.Controllers.Player;

// 인게임 상점 컨트롤러 (플레이어 전용)
// GET /api/shop          — 활성 상품 목록 조회
// POST /api/shop/{id}/buy — 상품 구매 (재화 차감 + 보상 지급)
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Game)]
[ApiController]
[Route("api/shop")]
public class ShopController : ControllerBase
{
    private readonly IShopPurchaseService _shopService;

    public ShopController(IShopPurchaseService shopService)
    {
        _shopService = shopService;
    }

    // 활성 상점 상품 목록 조회 — 클라이언트 상점 화면 표시용
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _shopService.GetActiveProductsAsync();
        return Ok(products);
    }

    // 상품 구매 처리 — 재화 차감 + 보상 지급
    // [멱등성] 클라이언트가 UUID를 생성하여 clientRequestId로 전달 — 동일 요청 중복 처리 방지
    [HttpPost("{productId:int}/buy")]
    public async Task<IActionResult> BuyProduct(int productId, [FromBody] ShopBuyRequest request)
    {
        // JWT 클레임에서 PlayerId 추출
        var playerId = User.GetPlayerIdRequired();

        var result = await _shopService.BuyAsync(playerId, productId, request.ClientRequestId, request.Quantity);

        return result.Status switch
        {
            // 정상 구매 완료
            ShopPurchaseStatus.Success => Ok(new { message = "구매가 완료되었습니다." }),

            // 상품 미존재 또는 비활성화 — 404
            ShopPurchaseStatus.ProductNotFound => NotFound(Problem(
                title: "상품 미존재",
                detail: "해당 상품을 찾을 수 없거나 현재 판매 중이 아닙니다.",
                statusCode: StatusCodes.Status404NotFound,
                type: "https://framework.api/errors/shop-product-not-found",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.ShopProductNotFound })),

            // 재화 부족 — 400
            ShopPurchaseStatus.NotEnoughCurrency => BadRequest(Problem(
                title: "재화 부족",
                detail: "구매에 필요한 재화가 부족합니다.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://framework.api/errors/shop-not-enough-currency",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.ShopNotEnoughCurrency })),

            // 구매 수량이 잔여 한도를 초과 — 400 (잔여 수량 포함 응답)
            ShopPurchaseStatus.LimitWouldExceed => BadRequest(Problem(
                title: "구매 한도 초과",
                detail: "요청 수량이 구매 가능한 잔여 한도를 초과합니다.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://framework.api/errors/shop-limit-would-exceed",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = ErrorCodes.ShopLimitWouldExceed,
                    ["remainingQuantity"] = result.RemainingQuantity
                })),

            // 1회 최대 구매 수량 초과 — 400
            ShopPurchaseStatus.MaxPerCallExceeded => BadRequest(Problem(
                title: "1회 구매 한도 초과",
                detail: "1회에 구매 가능한 최대 수량을 초과하였습니다.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://framework.api/errors/shop-max-per-call-exceeded",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.ShopMaxPerCallExceeded })),

            // 중복 요청 — 409
            ShopPurchaseStatus.Duplicate => Conflict(Problem(
                title: "중복 요청",
                detail: "이미 처리된 구매 요청입니다.",
                statusCode: StatusCodes.Status409Conflict,
                type: "https://framework.api/errors/shop-duplicate-request",
                extensions: new Dictionary<string, object?> { ["errorCode"] = ErrorCodes.ShopDuplicateRequest })),

            // 보상 테이블 비어있음 / 지급 실패 — 500
            ShopPurchaseStatus.RewardTableEmpty or ShopPurchaseStatus.RewardGrantFailed
                => StatusCode(StatusCodes.Status500InternalServerError, new { message = "구매 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요." }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "알 수 없는 오류가 발생했습니다." })
        };
    }
}
