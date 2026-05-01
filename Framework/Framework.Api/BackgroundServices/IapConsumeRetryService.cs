using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.Iap;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;

namespace Framework.Api.BackgroundServices;

// IAP consume 재시도 백그라운드 서비스
// Granted 상태이나 ConsumedAt이 null인 소모성 구매를 주기적으로 폴링하여 consume 재시도
// 지수 백오프: 2^ConsumeAttempts 분 대기 후 재시도. MaxAttempts 초과 시 AdminNotification + 중단
public class IapConsumeRetryService : BackgroundService
{
    // consume 재시도 최대 횟수 — 초과 시 AdminNotification 발송 후 중단
    private const int MaxAttempts = 10;

    // 폴링 주기 — 5분마다 pending consume 조회
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);

    // BackgroundService는 Singleton 수명 — Scoped 서비스(DB 등) 사용 시 IServiceScopeFactory로 스코프 생성
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IapConsumeRetryService> _logger;

    public IapConsumeRetryService(IServiceScopeFactory scopeFactory, ILogger<IapConsumeRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 서버 시작 직후 즉시 실행 방지 — 다른 Scoped 서비스 준비 완료 대기
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        // 취소 신호를 받을 때까지 폴링 반복
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingConsumesAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    // pending consume 일괄 처리 — 요청마다 새 스코프 생성하여 Scoped 서비스 주입
    private async Task ProcessPendingConsumesAsync(CancellationToken ct)
    {
        // BackgroundService는 Singleton이므로 매 폴링마다 새 스코프에서 Scoped 서비스 사용
        using var scope = _scopeFactory.CreateScope();
        var purchaseRepo = scope.ServiceProvider.GetRequiredService<IIapPurchaseRepository>();
        var consumerResolver = scope.ServiceProvider.GetRequiredService<IIapConsumerResolver>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IAdminNotificationService>();

        // pending consume 대상 조회 — 조회 실패 시 다음 폴링까지 대기
        List<IapPurchase> pending;
        try
        {
            pending = await purchaseRepo.FindPendingConsumesAsync(MaxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "consume 재시도 대상 조회 실패");
            return;
        }

        // 처리 대상 없으면 조기 반환
        if (pending.Count == 0) return;

        _logger.LogInformation("consume 재시도 대상 {Count}건 처리 시작", pending.Count);

        foreach (var purchase in pending)
        {
            // 취소 신호 수신 시 남은 항목 처리 중단
            if (ct.IsCancellationRequested) break;

            // 지수 백오프 계산 — 2^ConsumeAttempts 분이 경과하지 않으면 이번 폴링에서 건너뜀
            if (purchase.LastConsumeAttemptAt.HasValue)
            {
                var backoff = TimeSpan.FromMinutes(Math.Pow(2, purchase.ConsumeAttempts));
                if (DateTime.UtcNow - purchase.LastConsumeAttemptAt.Value < backoff)
                    continue;
            }

            await RetryConsumeAsync(purchase, consumerResolver, notificationService, purchaseRepo);
        }
    }

    // 단건 consume 재시도 — 성공/영구실패/일시실패 분기 처리
    private async Task RetryConsumeAsync(
        IapPurchase purchase,
        IIapConsumerResolver consumerResolver,
        IAdminNotificationService notificationService,
        IIapPurchaseRepository purchaseRepo)
    {
        try
        {
            // 스토어에 맞는 consume 구현체 조회 후 호출
            var consumer = consumerResolver.Resolve(purchase.Store);
            await consumer.ConsumeAsync(purchase.ProductId, purchase.PurchaseToken);

            // 성공 — ConsumedAt 기록하여 다음 폴링에서 제외
            purchase.ConsumedAt = DateTime.UtcNow;
            purchase.UpdatedAt = DateTime.UtcNow;
            await purchaseRepo.SaveChangesAsync();

            _logger.LogInformation("consume 재시도 성공 — PurchaseId: {Id}", purchase.Id);
        }
        catch (IapConsumeException ex)
        {
            // 시도 횟수 및 마지막 시도 시각 갱신
            purchase.ConsumeAttempts++;
            purchase.LastConsumeAttemptAt = DateTime.UtcNow;
            purchase.LastConsumeError = ex.Message;
            purchase.UpdatedAt = DateTime.UtcNow;

            if (ex.IsPermanent || purchase.ConsumeAttempts >= MaxAttempts)
            {
                // 영구실패 또는 최대 횟수 초과 — ConsumedAt 강제 마킹으로 retry 대상에서 제외 + Admin 알림
                purchase.ConsumedAt = DateTime.UtcNow;

                var reason = ex.IsPermanent ? "영구실패" : $"최대 {MaxAttempts}회 초과";
                _logger.LogError(ex,
                    "consume 재시도 포기 ({Reason}) — PurchaseId: {Id}, PlayerId: {Player}",
                    reason, purchase.Id, purchase.PlayerId);

                // Admin 알림 발송 실패해도 DB 저장은 진행
                try
                {
                    await notificationService.CreateAsync(
                        category: AdminNotificationCategory.IapConsumeFailure,
                        severity: AdminNotificationSeverity.Critical,
                        title: $"IAP consume 처리 실패 — PurchaseId {purchase.Id}",
                        message: $"소모성 상품 consume이 {reason}로 종료되었습니다. " +
                                 $"PlayerId={purchase.PlayerId}, ProductId={purchase.ProductId}. 수동 처리 필요.",
                        relatedEntityType: "IapPurchase",
                        relatedEntityId: purchase.Id,
                        dedupKey: $"iap-consume-fail:{purchase.Id}");
                }
                catch (Exception notifEx)
                {
                    // 알림 발송 실패는 로그만 남기고 DB 저장은 계속 진행
                    _logger.LogError(notifEx, "Admin 알림 발송 실패 — PurchaseId: {Id}", purchase.Id);
                }
            }
            else
            {
                // 일시실패 — 다음 폴링 사이클에서 지수 백오프 후 재시도
                _logger.LogWarning(ex,
                    "consume 일시실패 ({Attempt}/{Max}회) — PurchaseId: {Id}",
                    purchase.ConsumeAttempts, MaxAttempts, purchase.Id);
            }

            await purchaseRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // IapConsumeException 외의 예기치 않은 오류 — 이번 폴링에서 건너뜀
            _logger.LogError(ex, "consume 재시도 중 예기치 않은 오류 — PurchaseId: {Id}", purchase.Id);
        }
    }
}
