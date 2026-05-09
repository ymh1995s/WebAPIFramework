using Framework.Api.Extensions;
using Framework.Application.Features.Iap;
using Polly.Registry;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Api.Services.IapStore;

// Google Play purchases.products.consume 호출 구현체 — IIapConsumer Strategy
// 소모성 상품 consume 신고로 동일 purchaseToken 재구매 허용
// Polly "external-iap-consume" 파이프라인으로 타임아웃(15s)·재시도(2회)·서킷브레이커 적용
public class GooglePlayConsumer : IIapConsumer
{
    // 이 구현체가 담당하는 스토어
    public IapStoreEnum Store => IapStoreEnum.Google;

    private readonly GooglePlayClientFactory _clientFactory;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<GooglePlayConsumer> _logger;

    public GooglePlayConsumer(
        GooglePlayClientFactory clientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<GooglePlayConsumer> logger)
    {
        _clientFactory = clientFactory;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
    }

    // Google Play purchases.products.consume 호출
    // 영구실패(400/404/410): IapConsumeException(IsPermanent=true) 발생
    // 일시실패(500/503/기타): IapConsumeException(IsPermanent=false) 발생
    // BrokenCircuitException/TimeoutRejectedException: IapConsumeException(IsPermanent=false)로 변환
    //   → 기존 일시실패 경로(retry 워커 위임)와 동일하게 동작
    public async Task ConsumeAsync(string productId, string purchaseToken)
    {
        // Polly 파이프라인 로드 — DI에서 키로 조회
        var pipeline = _pipelineProvider.GetPipeline(ResilienceExtensions.IapConsumeKey);

        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await ConsumeInternalAsync(productId, purchaseToken, ct);
            });
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            // 서킷브레이커 OPEN — 일시실패로 처리하여 retry 워커에 위임
            _logger.LogWarning(
                "Google Play consume 서킷브레이커 OPEN — retry 워커 위임. ProductId: {ProductId}",
                productId);
            throw new IapConsumeException(IapStoreEnum.Google, "서킷브레이커 OPEN — 외부 스토어 서비스 일시 불가", isPermanent: false);
        }
        catch (Polly.Timeout.TimeoutRejectedException)
        {
            // Polly 타임아웃 초과 — 일시실패로 처리하여 retry 워커에 위임
            _logger.LogWarning(
                "Google Play consume 타임아웃 초과 — retry 워커 위임. ProductId: {ProductId}",
                productId);
            throw new IapConsumeException(IapStoreEnum.Google, "타임아웃 초과 — 외부 스토어 서비스 응답 지연", isPermanent: false);
        }
    }

    // 실제 Google Play API 호출 — Polly 콜백 내부 실행
    private async Task ConsumeInternalAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        // appsettings에서 PackageName 로드
        var packageName = _clientFactory.GetPackageName();

        try
        {
            // AndroidPublisherService 생성 — using으로 요청 완료 후 즉시 dispose
            using var service = _clientFactory.Create();

            // purchases.products.consume 호출 — 응답 바디 없는 void 호출
            // R-1 대응: ExecuteAsync가 CancellationToken을 지원하지 않으므로 WaitAsync로 래핑
            await service.Purchases.Products.Consume(packageName, productId, purchaseToken)
                .ExecuteAsync().WaitAsync(ct);

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
        catch (Exception ex) when (ex is not IapConsumeException and not OperationCanceledException)
        {
            // 파일 읽기 실패, 네트워크 오류 등 예기치 않은 오류 — 일시적 오류로 처리
            // OperationCanceledException은 Polly 타임아웃 신호 — 그대로 전파
            _logger.LogWarning(ex, "Google Play consume 예기치 않은 오류 — ProductId: {ProductId}", productId);
            throw new IapConsumeException(IapStoreEnum.Google, ex.Message, isPermanent: false, ex);
        }
    }
}
