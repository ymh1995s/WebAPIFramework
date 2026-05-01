using Framework.Application.Features.Iap;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Api.Services.IapStore;

// Google Play purchases.products.consume 호출 구현체 — IIapConsumer Strategy
// 소모성 상품 consume 신고로 동일 purchaseToken 재구매 허용
public class GooglePlayConsumer : IIapConsumer
{
    // 이 구현체가 담당하는 스토어
    public IapStoreEnum Store => IapStoreEnum.Google;

    private readonly GooglePlayClientFactory _clientFactory;
    private readonly ILogger<GooglePlayConsumer> _logger;

    public GooglePlayConsumer(GooglePlayClientFactory clientFactory, ILogger<GooglePlayConsumer> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    // Google Play purchases.products.consume 호출
    // 영구실패(400/404/410): IapConsumeException(IsPermanent=true) 발생
    // 일시실패(500/503/기타): IapConsumeException(IsPermanent=false) 발생
    public async Task ConsumeAsync(string productId, string purchaseToken)
    {
        // appsettings에서 PackageName 로드
        var packageName = _clientFactory.GetPackageName();

        try
        {
            // AndroidPublisherService 생성 — using으로 요청 완료 후 즉시 dispose
            using var service = await _clientFactory.CreateAsync();

            // purchases.products.consume 호출 — 응답 바디 없는 void 호출
            await service.Purchases.Products.Consume(packageName, productId, purchaseToken).ExecuteAsync();

            _logger.LogInformation(
                "Google Play consume 성공 — ProductId: {ProductId}",
                productId);
        }
        catch (Google.GoogleApiException apiEx)
        {
            // 영구실패 판정: 400(잘못된 요청) / 404(토큰 없음) / 410(이미 소비됨 또는 만료)
            var isPermanent = apiEx.HttpStatusCode is
                System.Net.HttpStatusCode.BadRequest or
                System.Net.HttpStatusCode.NotFound or
                System.Net.HttpStatusCode.Gone;

            _logger.LogWarning(
                apiEx,
                "Google Play consume 실패 — ProductId: {ProductId}, StatusCode: {Status}, 영구실패: {Permanent}",
                productId, apiEx.HttpStatusCode, isPermanent);

            throw new IapConsumeException(IapStoreEnum.Google, apiEx.Message, isPermanent, apiEx);
        }
        catch (Exception ex)
        {
            // 파일 읽기 실패, 네트워크 오류 등 예기치 않은 오류 — 일시적 오류로 처리
            _logger.LogWarning(ex, "Google Play consume 예기치 않은 오류 — ProductId: {ProductId}", productId);
            throw new IapConsumeException(IapStoreEnum.Google, ex.Message, isPermanent: false, ex);
        }
    }
}
