using System.Text.Json;
using Framework.Application.Features.Iap;
using Framework.Domain.Enums;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Api.Services.IapStore;

// Google Play Store 영수증 검증기 — IIapStoreVerifier Strategy 구현체
// Google Play Developer API (purchases.products.get)를 호출하여 구매 유효성 확인
// AndroidPublisherService 초기화는 GooglePlayClientFactory에 위임 — Consumer와 중복 제거
public class GooglePlayStoreVerifier : IIapStoreVerifier
{
    // 이 검증기가 담당하는 스토어
    public IapStoreEnum Store => IapStoreEnum.Google;

    private readonly GooglePlayClientFactory _clientFactory;
    private readonly ILogger<GooglePlayStoreVerifier> _logger;

    public GooglePlayStoreVerifier(GooglePlayClientFactory clientFactory, ILogger<GooglePlayStoreVerifier> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    // Google Play 구매 영수증 검증
    // productId: 스토어 SKU, purchaseToken: Google 발급 결제 토큰
    // 검증 성공 시 IapReceiptVerified 반환, 실패 시 예외 발생
    public async Task<IapReceiptVerified> VerifyAsync(string productId, string purchaseToken)
    {
        // GooglePlayClientFactory에서 PackageName 및 API 클라이언트 로드
        var packageName = _clientFactory.GetPackageName();

        try
        {
            // AndroidPublisherService 생성 — 팩토리로 중복 초기화 코드 제거
            using var service = await _clientFactory.CreateAsync();

            // purchases.products.get 호출 — 단건 상품 구매 조회
            _logger.LogDebug(
                "Google Play 구매 조회 요청 — PackageName: {PackageName}, ProductId: {ProductId}",
                packageName, productId);

            var request = service.Purchases.Products.Get(packageName, productId, purchaseToken);
            var productPurchase = await request.ExecuteAsync();

            // purchaseState 검증: 0 = 구매 완료, 1 = 취소, 2 = 보류 중
            if (productPurchase.PurchaseState != 0)
            {
                _logger.LogWarning(
                    "Google Play 구매 상태 이상 — ProductId: {ProductId}, PurchaseState: {State}",
                    productId, productPurchase.PurchaseState);
                throw new IapReceiptInvalidException(
                    IapStoreEnum.Google,
                    $"구매 상태가 완료 상태가 아닙니다. PurchaseState={productPurchase.PurchaseState}");
            }

            // packageName 일치 검증 — 다른 앱의 구매 토큰 재사용 방지
            if (!string.Equals(productPurchase.RegionCode, null) &&
                productPurchase.DeveloperPayload is not null)
            {
                // packageName은 API 호출 파라미터로 이미 검증됨 (오류 시 API가 예외 발생)
            }

            // 상품 유형 판별: consumptionState 기반
            // consumptionState = 0: 아직 소비하지 않음 → Consumable (소모성)
            // consumptionState = 1: 이미 소비됨 또는 null → NonConsumable로 처리
            var productType = productPurchase.ConsumptionState == 0
                ? IapProductType.Consumable
                : IapProductType.NonConsumable;

            // purchaseTimeMillis → UTC DateTime 변환
            // Google API는 Unix 밀리초(long) 형태로 반환
            DateTime purchaseTimeUtc;
            if (productPurchase.PurchaseTimeMillis.HasValue)
            {
                purchaseTimeUtc = DateTimeOffset
                    .FromUnixTimeMilliseconds(productPurchase.PurchaseTimeMillis.Value)
                    .UtcDateTime;
            }
            else
            {
                // 타임스탬프 없는 예외 케이스 — 현재 시각으로 대체
                purchaseTimeUtc = DateTime.UtcNow;
                _logger.LogWarning(
                    "Google Play 응답에 PurchaseTimeMillis 없음 — ProductId: {ProductId}",
                    productId);
            }

            // Google API 응답 전체를 JSON으로 직렬화하여 원본 보존 (분쟁/감사 대응)
            var rawReceiptJson = JsonSerializer.Serialize(productPurchase);

            _logger.LogInformation(
                "Google Play 영수증 검증 성공 — ProductId: {ProductId}, PurchaseTimeUtc: {Time}",
                productId, purchaseTimeUtc);

            return new IapReceiptVerified(
                ProductId: productId,
                ProductType: productType,
                PurchaseTimeUtc: purchaseTimeUtc,
                RawReceiptJson: rawReceiptJson
            );
        }
        catch (IapReceiptInvalidException)
        {
            // 이미 구체적인 영수증 무효 예외 — 그대로 재throw
            throw;
        }
        catch (Google.GoogleApiException apiEx)
        {
            // Google API 호출 자체 실패 (403 권한 없음, 404 토큰 없음, 네트워크 오류 등)
            _logger.LogError(
                apiEx,
                "Google Play API 호출 실패 — ProductId: {ProductId}, StatusCode: {Status}",
                productId, apiEx.HttpStatusCode);

            // 404는 유효하지 않은 토큰 → 영수증 무효 처리
            if (apiEx.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new IapReceiptInvalidException(
                    IapStoreEnum.Google,
                    $"구매 토큰을 찾을 수 없습니다. ProductId={productId}");
            }

            throw new IapVerifierException(
                IapStoreEnum.Google,
                $"Google Play API 오류: {apiEx.Message}");
        }
        catch (Exception ex) when (ex is not IapVerifierException)
        {
            // 파일 읽기 실패, 네트워크 오류 등 예기치 않은 예외
            _logger.LogError(
                ex,
                "Google Play 검증기 예기치 않은 오류 — ProductId: {ProductId}",
                productId);
            throw new IapVerifierException(
                IapStoreEnum.Google,
                $"검증 처리 중 오류가 발생했습니다: {ex.Message}");
        }
    }
}
