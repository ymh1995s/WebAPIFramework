using Framework.Application.Common;
using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.Reward;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Iap;

// 인앱결제 메인 처리 서비스 — 검증 + 보상 지급 파이프라인
// [흐름] 상품 조회 → 중복 방지 → 트랜잭션 시작 → Pending 저장 → 외부 검증 → 보상 지급 → Granted
// 소모성 상품의 경우 트랜잭션 커밋 후 consume 호출, 실패 시 retry 워커 위임
public class IapPurchaseService : IIapPurchaseService
{
    private readonly IIapProductRepository _productRepo;
    private readonly IIapPurchaseRepository _purchaseRepo;
    private readonly IIapStoreVerifierResolver _verifierResolver;
    private readonly IIapConsumerResolver _consumerResolver;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IapPurchaseService> _logger;
    private readonly IAdminNotificationService _notificationService;

    // 동시성 충돌 시 최대 재시도 횟수 — H-3 라운드 RewardDispatcher/MailService와 동일
    private const int MaxConcurrencyRetries = 3;

    public IapPurchaseService(
        IIapProductRepository productRepo,
        IIapPurchaseRepository purchaseRepo,
        IIapStoreVerifierResolver verifierResolver,
        IIapConsumerResolver consumerResolver,
        IRewardDispatcher rewardDispatcher,
        IRewardTableRepository rewardTableRepo,
        IUnitOfWork unitOfWork,
        ILogger<IapPurchaseService> logger,
        IAdminNotificationService notificationService)
    {
        _productRepo = productRepo;
        _purchaseRepo = purchaseRepo;
        _verifierResolver = verifierResolver;
        _consumerResolver = consumerResolver;
        _rewardDispatcher = rewardDispatcher;
        _rewardTableRepo = rewardTableRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _notificationService = notificationService;
    }

    // 구매 영수증 검증 후 보상 지급 — 멱등성 보장 (동일 토큰 재요청 처리)
    // [흐름] 트랜잭션 외부: 상품 조회 + 중복 체크 → 트랜잭션 스코프: Pending 저장 → 검증 → 보상 지급 → Granted
    //        → 트랜잭션 커밋 후: Consumable이면 consume 호출 (실패해도 유저 응답 불변)
    // [M-29] DbUpdateConcurrencyException 시 최대 3회 재시도 — verify ↔ RTDN Lost Update 차단
    public async Task<IapVerifyResult> VerifyAndGrantAsync(
        int playerId, IapVerifyRequest request, string? clientIp)
    {
        // (a) 활성 상품 조회 — 트랜잭션 외부
        var product = await _productRepo.FindActiveAsync(IapStore.Google, request.ProductId);
        if (product is null)
        {
            _logger.LogWarning("인앱결제 상품 없음 — Store: Google, ProductId: {ProductId}", request.ProductId);
            throw new IapProductNotFoundException(IapStore.Google, request.ProductId);
        }

        // (b) 동일 구매 토큰 중복 처리 체크 — 트랜잭션 외부
        var existingPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);
        if (existingPurchase is not null)
        {
            if (existingPurchase.PlayerId != playerId)
            {
                _logger.LogWarning(
                    "구매 토큰 소유자 불일치 — 요청 PlayerId: {RequestPlayer}, 토큰 소유자: {TokenOwner}",
                    playerId, existingPurchase.PlayerId);
                throw new IapTokenOwnershipMismatchException(IapStore.Google, request.PurchaseToken);
            }

            if (existingPurchase.Status == IapPurchaseStatus.Granted)
            {
                _logger.LogInformation(
                    "중복 구매 요청 (이미 지급됨) — PlayerId: {PlayerId}, PurchaseToken: {Token}",
                    playerId, MaskToken(request.PurchaseToken));
                return new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: existingPurchase.Id);
            }
        }

        // consume 호출이 필요한 경우 트랜잭션 외부에서 처리하기 위해 변수 선언
        Framework.Domain.Entities.IapPurchase? purchaseForConsume = null;

        // (c)~(i) 트랜잭션 스코프 — 3회 재시도 루프 (M-29 낙관적 동시성 충돌 대응)
        // 매 시도 진입 시 DB 최신값 재로드 → Status 분기로 이어서 처리
        IapVerifyResult result = default!;

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                // 트랜잭션 스코프 내 처리 — 반환 튜플로 purchaseForConsume 전달
                (result, purchaseForConsume) = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // 매 시도 진입 시 DB 최신값 재로드 — 동시성 충돌 후 stale 데이터 방지 (단순화 옵션)
                    var currentPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);

                    // 기존 purchase가 있는 경우 — Status 분기
                    if (currentPurchase is not null)
                    {
                        switch (currentPurchase.Status)
                        {
                            case IapPurchaseStatus.Refunded:
                                // 검증 도중 RTDN 환불이 먼저 완주 — 보상 지급 중단, 정상 응답
                                _logger.LogInformation(
                                    "IapPurchase 환불 감지 — verify 중단 (PlayerId: {PlayerId}, PurchaseId: {PurchaseId})",
                                    playerId, currentPurchase.Id);
                                return (new IapVerifyResult(Ok: true, AlreadyGranted: false, PurchaseId: currentPurchase.Id),
                                        (Framework.Domain.Entities.IapPurchase?)null);

                            case IapPurchaseStatus.Granted:
                                // 다른 인스턴스가 이미 완주 — AlreadyGranted 응답
                                _logger.LogInformation(
                                    "중복 구매 요청 (이미 지급됨) — PlayerId: {PlayerId}, PurchaseToken: {Token}",
                                    playerId, MaskToken(request.PurchaseToken));
                                return (new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: currentPurchase.Id),
                                        (Framework.Domain.Entities.IapPurchase?)null);

                            case IapPurchaseStatus.Verified:
                            case IapPurchaseStatus.Pending:
                                // Pending: 검증부터 재수행 / Verified: 보상 지급부터 재수행
                                // 두 경우 모두 아래 공통 처리 블록으로 fall-through
                                break;
                        }

                        // Pending/Verified 상태 — 외부 검증 + 보상 지급 수행
                        return await ExecuteCoreAsync(currentPurchase);
                    }

                    // 기존 purchase 없음 — 신규 Pending INSERT 후 검증 진행
                    var newPurchase = new Framework.Domain.Entities.IapPurchase
                    {
                        PlayerId = playerId,
                        Store = IapStore.Google,
                        ProductId = request.ProductId,
                        PurchaseToken = request.PurchaseToken,
                        OrderId = request.OrderId,
                        Status = IapPurchaseStatus.Pending,
                        ProductType = product.ProductType, // 상품 유형 스냅샷 — retry 워커 필터링용
                        ClientIp = clientIp,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _purchaseRepo.AddAsync(newPurchase);

                    try
                    {
                        await _purchaseRepo.SaveChangesAsync();
                    }
                    catch (DbUpdateException dbEx) when (dbEx.IsUniqueViolation())
                    {
                        // 동시 요청 경쟁 — ChangeTracker 정리 후 기존 레코드 상태 확인
                        _unitOfWork.DetachEntry(newPurchase);
                        var concurrentPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);
                        if (concurrentPurchase?.Status == IapPurchaseStatus.Granted)
                            return (new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: concurrentPurchase.Id),
                                    (Framework.Domain.Entities.IapPurchase?)null);

                        _logger.LogWarning(
                            "중복 구매 토큰 저장 시도 충돌 — PlayerId: {PlayerId}, Token: {Token}",
                            playerId, MaskToken(request.PurchaseToken));
                        throw new IapReceiptInvalidException(IapStore.Google, "동시 처리 중인 구매입니다. 잠시 후 다시 시도해주세요.");
                    }

                    // 신규 purchase 검증 + 보상 지급
                    return await ExecuteCoreAsync(newPurchase);

                    // 검증 + 보상 지급 공통 처리 — purchase의 Status에 따라 분기
                    async Task<(IapVerifyResult, Framework.Domain.Entities.IapPurchase?)> ExecuteCoreAsync(
                        Framework.Domain.Entities.IapPurchase purchase)
                    {
                        // 외부 스토어 검증 — Pending 상태인 경우에만 수행 (Verified는 이미 완료)
                        if (purchase.Status == IapPurchaseStatus.Pending)
                        {
                            IapReceiptVerified verified;
                            try
                            {
                                var verifier = _verifierResolver.Resolve(IapStore.Google);
                                verified = await verifier.VerifyAsync(request.ProductId, request.PurchaseToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Google Play 검증 실패 — PlayerId: {PlayerId}, ProductId: {ProductId}",
                                    playerId, request.ProductId);
                                throw;
                            }

                            // Verified 상태 갱신
                            purchase.Status = IapPurchaseStatus.Verified;
                            purchase.RawReceipt = verified.RawReceiptJson;
                            purchase.PurchaseTimeUtc = verified.PurchaseTimeUtc;
                            purchase.VerifiedAt = DateTime.UtcNow;
                            purchase.UpdatedAt = DateTime.UtcNow;
                        }

                        // 보상 번들 구성
                        var bundle = await RewardBundleBuilder.BuildAsync(_rewardTableRepo, product.RewardTableId);

                        // 보상 지급 — RewardDispatcher가 자동으로 참여자가 되어 중첩 트랜잭션 없음
                        var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
                            PlayerId: playerId,
                            SourceType: RewardSourceType.Purchase,
                            SourceKey: SourceKeys.IapPurchase("google", request.PurchaseToken),
                            Bundle: bundle,
                            MailTitle: "인앱결제 보상",
                            MailBody: $"'{product.Description}' 상품 구매에 감사드립니다. 보상을 수령해 주세요.",
                            Mode: DispatchMode.Direct,
                            // 플레이어가 직접 결제한 보상 — 행위자는 Player
                            ActorType: AuditActorType.Player,
                            ActorId: playerId
                        ));

                        if (!grantResult.Success && !grantResult.AlreadyGranted)
                        {
                            _logger.LogError(
                                "보상 지급 실패 — PlayerId: {PlayerId}, PurchaseId: {Id}, 이유: {Reason}",
                                playerId, purchase.Id, grantResult.Message);
                            throw new InvalidOperationException($"보상 지급에 실패했습니다: {grantResult.Message}");
                        }

                        // Granted 상태 갱신
                        purchase.Status = IapPurchaseStatus.Granted;
                        purchase.RewardTableIdSnapshot = product.RewardTableId;
                        purchase.GrantedAt = DateTime.UtcNow;
                        purchase.UpdatedAt = DateTime.UtcNow;
                        await _purchaseRepo.SaveChangesAsync();

                        _logger.LogInformation(
                            "인앱결제 처리 완료 — PlayerId: {PlayerId}, PurchaseId: {Id}, ProductId: {ProductId}, AlreadyGranted: {Already}",
                            playerId, purchase.Id, request.ProductId, grantResult.AlreadyGranted);

                        // Consumable 상품은 트랜잭션 커밋 후 consume 호출 대상으로 반환
                        Framework.Domain.Entities.IapPurchase? consumeTarget =
                            product.ProductType == IapProductType.Consumable ? purchase : null;

                        return (new IapVerifyResult(Ok: true, AlreadyGranted: grantResult.AlreadyGranted, PurchaseId: purchase.Id),
                                consumeTarget);
                    }
                });

                // 정상 완료 — 루프 탈출
                break;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 동시성 충돌 — ChangeTracker 정리 후 재시도
                _logger.LogWarning(
                    ex,
                    "IapPurchase 동시성 충돌 — 재시도 {Attempt}/{Max} (PurchaseToken: {Token})",
                    attempt, MaxConcurrencyRetries, MaskToken(request.PurchaseToken));

                _unitOfWork.ClearChangeTracker();

                if (attempt < MaxConcurrencyRetries)
                    continue;

                // 재시도 한도 초과 — AdminNotification 발송 후 503 예외
                _logger.LogError(
                    ex,
                    "IapPurchase verify 동시성 충돌 한도 초과 — PlayerId: {PlayerId}, Token: {Token}",
                    playerId, MaskToken(request.PurchaseToken));

                try
                {
                    await _notificationService.CreateAsync(
                        category: AdminNotificationCategory.IapVerifyConcurrencyExhausted,
                        severity: AdminNotificationSeverity.Critical,
                        title: $"IAP verify 동시성 충돌 한도 초과 — PlayerId {playerId}",
                        message: $"IAP verify 경로에서 동시성 충돌이 {MaxConcurrencyRetries}회 발생했습니다. " +
                                 $"PlayerId={playerId}, ProductId={request.ProductId}. 수동 확인 필요.",
                        relatedEntityType: "IapPurchase",
                        relatedEntityId: playerId,
                        dedupKey: AdminNotificationDedupKeys.IapVerifyConcurrencyExhausted(MaskToken(request.PurchaseToken)));
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, "Admin 알림 발송 실패 (동시성 한도 초과) — PlayerId: {PlayerId}", playerId);
                }

                throw new IapVerifyConcurrencyException(IapStore.Google, MaskToken(request.PurchaseToken));
            }
        }

        // (k) 트랜잭션 커밋 완료 후 consume 호출 — 실패해도 유저 응답 불변, retry 워커 위임
        if (purchaseForConsume is not null)
            await ExecuteConsumeAsync(purchaseForConsume, request.ProductId, request.PurchaseToken);

        return result;
    }

    // 트랜잭션 커밋 후 Google Play consume 호출 — 실패해도 유저 응답 불변
    // 영구실패: ConsumedAt 마킹 후 중단. 일시실패: 시도 횟수 증가 + retry 워커 위임
    private async Task ExecuteConsumeAsync(
        Framework.Domain.Entities.IapPurchase purchase, string productId, string purchaseToken)
    {
        try
        {
            var consumer = _consumerResolver.Resolve(IapStore.Google);
            await consumer.ConsumeAsync(productId, purchaseToken);

            // 성공 — ConsumedAt 기록
            purchase.ConsumedAt = DateTime.UtcNow;
            purchase.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _purchaseRepo.SaveChangesAsync();
                _logger.LogInformation("consume 완료 — PurchaseId: {Id}", purchase.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                // [동시성 최소 패턴] ConsumedAt 갱신 중 RTDN 등 외부 갱신과 충돌 시
                // 1회 재시도 + 재로드 가드 — verify 본 경로(3회)와 달리 retry 워커가 안전망이므로 최소 처리
                _logger.LogWarning("consume 성공 후 ConsumedAt 갱신 동시성 충돌 — 재로드 후 1회 재시도. PurchaseId: {Id}", purchase.Id);
                _unitOfWork.ClearChangeTracker();

                var reloaded = await _purchaseRepo.FindByTokenAsync(IapStore.Google, purchaseToken);
                // 재로드 가드 — 다른 인스턴스가 이미 처리했거나 환불된 경우 재시도 불필요
                if (reloaded is null || reloaded.ConsumedAt is not null || reloaded.Status == IapPurchaseStatus.Refunded)
                {
                    _logger.LogInformation("consume ConsumedAt 갱신 재시도 불필요 (이미 처리 or 환불) — PurchaseId: {Id}", purchase.Id);
                    return;
                }

                reloaded.ConsumedAt = DateTime.UtcNow;
                reloaded.UpdatedAt = DateTime.UtcNow;
                try
                {
                    await _purchaseRepo.SaveChangesAsync();
                    _logger.LogInformation("consume 완료 (재시도 성공) — PurchaseId: {Id}", purchase.Id);
                }
                catch (DbUpdateConcurrencyException ex2)
                {
                    // 1회 재시도도 실패 — 예외 전파 없이 종료. retry 워커가 다음 사이클에 후속 처리
                    _logger.LogError(ex2, "consume ConsumedAt 갱신 재시도 실패 — retry 워커 위임. PurchaseId: {Id}", purchase.Id);
                }
            }
        }
        catch (IapConsumeException ex)
        {
            // 시도 횟수 및 마지막 시도 시각 갱신
            purchase.ConsumeAttempts++;
            purchase.LastConsumeAttemptAt = DateTime.UtcNow;
            purchase.LastConsumeError = ex.Message;
            purchase.UpdatedAt = DateTime.UtcNow;

            if (ex.IsPermanent)
            {
                // 영구실패: 재시도 무의미 — ConsumedAt 강제 마킹으로 retry 워커 제외
                purchase.ConsumedAt = DateTime.UtcNow;
                _logger.LogError(ex,
                    "consume 영구실패 (재시도 중단) — PurchaseId: {Id}, ProductId: {ProductId}",
                    purchase.Id, productId);

                // retry 워커 대상에서 영구 제외되므로 Admin에 즉시 알림 발송
                try
                {
                    await _notificationService.CreateAsync(
                        category: AdminNotificationCategory.IapConsumeFailure,
                        severity: AdminNotificationSeverity.Critical,
                        title: $"IAP consume 영구실패 — PurchaseId {purchase.Id}",
                        message: $"소모성 상품 consume이 영구실패로 종료되었습니다. " +
                                 $"PlayerId={purchase.PlayerId}, ProductId={productId}. 수동 처리 필요.",
                        relatedEntityType: "IapPurchase",
                        relatedEntityId: purchase.Id,
                        dedupKey: AdminNotificationDedupKeys.IapConsumeFail(purchase.Id));
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, "Admin 알림 발송 실패 — PurchaseId: {Id}", purchase.Id);
                }
            }
            else
            {
                // 일시실패: retry 워커가 후속 처리 — ConsumedAt 미기록으로 폴링 대상 유지
                _logger.LogWarning(ex,
                    "consume 일시실패 (retry 워커 위임) — PurchaseId: {Id}",
                    purchase.Id);
            }

            try
            {
                await _purchaseRepo.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // [동시성 최소 패턴] consume 실패 메타 갱신 중 충돌 시 1회 재시도
                _logger.LogWarning("consume 실패 메타 갱신 동시성 충돌 — 재로드 후 1회 재시도. PurchaseId: {Id}", purchase.Id);
                _unitOfWork.ClearChangeTracker();

                var reloaded = await _purchaseRepo.FindByTokenAsync(IapStore.Google, purchaseToken);
                // 재로드 가드 — 이미 처리됐거나 환불된 경우 재시도 불필요
                if (reloaded is null || reloaded.Status == IapPurchaseStatus.Refunded || reloaded.ConsumedAt is not null)
                {
                    _logger.LogInformation("consume 실패 메타 갱신 재시도 불필요 (이미 처리 or 환불) — PurchaseId: {Id}", purchase.Id);
                    return;
                }

                reloaded.ConsumeAttempts++;
                reloaded.LastConsumeAttemptAt = DateTime.UtcNow;
                reloaded.LastConsumeError = ex.Message;
                reloaded.UpdatedAt = DateTime.UtcNow;
                if (ex.IsPermanent)
                    reloaded.ConsumedAt = DateTime.UtcNow;

                try
                {
                    await _purchaseRepo.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException exRetry)
                {
                    // 재시도 실패 — 예외 전파 없이 종료. 다음 RTDN/워커 사이클에 위임
                    _logger.LogError(exRetry, "consume 실패 메타 재시도 실패 — 워커 위임. PurchaseId: {Id}", purchase.Id);
                }
            }
        }
    }

    // 로그에 구매 토큰 전체 노출 방지 — 앞 8자만 표시
    private static string MaskToken(string token)
        => token.Length > 8 ? token[..8] + "..." : "***";
}
