using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Application.Features.Iap;

// RTDN 알림 처리 서비스 구현체
// Google Play 환불/취소 알림 수신 시 IapPurchase.Status를 Refunded로 갱신
public class IapRtdnService : IIapRtdnService
{
    private readonly IIapPurchaseRepository _purchaseRepository;
    private readonly ILogger<IapRtdnService> _logger;

    public IapRtdnService(
        IIapPurchaseRepository purchaseRepository,
        ILogger<IapRtdnService> logger)
    {
        _purchaseRepository = purchaseRepository;
        _logger = logger;
    }

    // RTDN 페이로드 알림 유형 분기 처리
    // 우선순위: TestNotification → VoidedPurchase → OneTimeProduct(CANCELED) → 그 외(무시)
    public async Task HandleAsync(RtdnPayload payload)
    {
        // Play Console 테스트 알림 — 실제 처리 없이 로그만 기록
        if (payload.TestNotification is not null)
        {
            _logger.LogInformation(
                "RTDN 테스트 알림 수신 — PackageName: {PackageName}, Version: {Version}",
                payload.PackageName, payload.TestNotification.Version);
            return;
        }

        // Voided Purchase(강제 환불) 알림 처리 — Google이 직접 환불 처리한 경우
        if (payload.VoidedPurchaseNotification is not null)
        {
            await HandleVoidedAsync(payload.VoidedPurchaseNotification);
            return;
        }

        // OneTimeProduct 취소 알림 (NotificationType=2) 처리
        if (payload.OneTimeProductNotification is not null)
        {
            if (payload.OneTimeProductNotification.NotificationType == 2)
            {
                await HandleCanceledAsync(payload.OneTimeProductNotification);
            }
            else
            {
                // PURCHASED(1) 등 그 외 유형 — 클라이언트 검증 흐름에서 처리되므로 무시
                _logger.LogDebug(
                    "RTDN OneTimeProduct 알림 무시 — NotificationType: {Type}, Sku: {Sku}",
                    payload.OneTimeProductNotification.NotificationType,
                    payload.OneTimeProductNotification.Sku);
            }
            return;
        }

        // 알 수 없는 알림 유형 — 미래 Google 알림 유형 추가 대비 경고 로그
        _logger.LogWarning(
            "RTDN 알 수 없는 알림 유형 수신 — PackageName: {PackageName}, EventTime: {EventTime}",
            payload.PackageName, payload.EventTimeMillis);
    }

    // Voided Purchase 환불 처리 — Google Voided Purchase API로 강제 환불된 구매 처리
    private async Task HandleVoidedAsync(VoidedPurchaseNotification notification)
    {
        // 구매 토큰으로 기존 이력 조회
        var purchase = await _purchaseRepository.FindByTokenAsync(
            IapStoreEnum.Google, notification.PurchaseToken);

        if (purchase is null)
        {
            // 서버에서 검증한 적 없는 토큰 — 외부 경로 환불이거나 데이터 불일치
            _logger.LogWarning(
                "RTDN Voided 환불 — 구매 이력 없음. PurchaseToken: {Token}, OrderId: {OrderId}",
                notification.PurchaseToken, notification.OrderId);
            return;
        }

        // 이미 환불 처리된 경우 중복 처리 방지
        if (purchase.Status == IapPurchaseStatus.Refunded)
        {
            _logger.LogInformation(
                "RTDN Voided 환불 — 이미 처리됨. IapPurchaseId: {Id}", purchase.Id);
            return;
        }

        // 상태를 Refunded로 변경하고 환불 시각 기록
        purchase.Status = IapPurchaseStatus.Refunded;
        purchase.RefundedAt = DateTime.UtcNow;
        purchase.UpdatedAt = DateTime.UtcNow;

        await _purchaseRepository.SaveChangesAsync();

        _logger.LogWarning(
            "RTDN 환불 감지 — PlayerId: {PlayerId}, ProductId: {ProductId}, OrderId: {OrderId}",
            purchase.PlayerId, purchase.ProductId, notification.OrderId);
    }

    // OneTimeProduct CANCELED(취소) 처리 — 구매 취소/환불 시 발생
    private async Task HandleCanceledAsync(OneTimeProductNotification notification)
    {
        // 구매 토큰으로 기존 이력 조회
        var purchase = await _purchaseRepository.FindByTokenAsync(
            IapStoreEnum.Google, notification.PurchaseToken);

        if (purchase is null)
        {
            // 서버에서 검증한 적 없는 토큰 — 무시
            _logger.LogWarning(
                "RTDN Canceled 환불 — 구매 이력 없음. PurchaseToken: {Token}, Sku: {Sku}",
                notification.PurchaseToken, notification.Sku);
            return;
        }

        // 이미 환불 처리된 경우 중복 처리 방지
        if (purchase.Status == IapPurchaseStatus.Refunded)
        {
            _logger.LogInformation(
                "RTDN Canceled 환불 — 이미 처리됨. IapPurchaseId: {Id}", purchase.Id);
            return;
        }

        // 상태를 Refunded로 변경하고 환불 시각 기록
        purchase.Status = IapPurchaseStatus.Refunded;
        purchase.RefundedAt = DateTime.UtcNow;
        purchase.UpdatedAt = DateTime.UtcNow;

        await _purchaseRepository.SaveChangesAsync();

        _logger.LogWarning(
            "RTDN 환불 감지 — PlayerId: {PlayerId}, ProductId: {ProductId}, OrderId: {OrderId}",
            purchase.PlayerId, purchase.ProductId, purchase.OrderId);
    }
}
