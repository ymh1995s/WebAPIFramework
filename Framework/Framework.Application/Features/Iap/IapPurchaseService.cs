using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.Reward;
using Framework.Domain.Entities;
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
    // RewardDispatcher도 ExecuteInTransactionAsync 사용 → 자동으로 참여자가 되어 중첩 트랜잭션 없음
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
        IapPurchase? purchaseForConsume = null;

        // (c)~(i) 트랜잭션 스코프 — Pending 저장부터 Granted까지 원자적 처리
        // RewardDispatcher도 ExecuteInTransactionAsync 사용 → 자동으로 참여자가 되어 중첩 트랜잭션 없음
        var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // (d) 구매 이력 Pending 상태로 먼저 저장
            var purchase = new IapPurchase
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
            await _purchaseRepo.AddAsync(purchase);

            try
            {
                await _purchaseRepo.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                // 동시 요청 경쟁 — ChangeTracker 정리 후 기존 레코드 상태 확인
                _unitOfWork.DetachEntry(purchase);
                var concurrentPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);
                if (concurrentPurchase?.Status == IapPurchaseStatus.Granted)
                    return new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: concurrentPurchase.Id);

                _logger.LogWarning(
                    "중복 구매 토큰 저장 시도 충돌 — PlayerId: {PlayerId}, Token: {Token}",
                    playerId, MaskToken(request.PurchaseToken));
                throw new IapReceiptInvalidException(IapStore.Google, "동시 처리 중인 구매입니다. 잠시 후 다시 시도해주세요.");
            }

            // (e) 외부 스토어 검증 API 호출 — 실패 시 예외 전파, ExecuteInTransactionAsync가 자동 롤백
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

            // (f) Verified 상태 갱신
            purchase.Status = IapPurchaseStatus.Verified;
            purchase.RawReceipt = verified.RawReceiptJson;
            purchase.PurchaseTimeUtc = verified.PurchaseTimeUtc;
            purchase.VerifiedAt = DateTime.UtcNow;
            purchase.UpdatedAt = DateTime.UtcNow;

            // (g) 보상 번들 구성
            var bundle = await BuildBundleAsync(product.RewardTableId);

            // (h) 보상 지급 — RewardDispatcher가 자동으로 참여자가 되어 중첩 트랜잭션 없음
            var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
                PlayerId: playerId,
                SourceType: RewardSourceType.Purchase,
                SourceKey: $"google:{request.PurchaseToken}",
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

            // (i) Granted 상태 갱신
            purchase.Status = IapPurchaseStatus.Granted;
            purchase.RewardTableIdSnapshot = product.RewardTableId;
            purchase.GrantedAt = DateTime.UtcNow;
            purchase.UpdatedAt = DateTime.UtcNow;
            await _purchaseRepo.SaveChangesAsync();

            _logger.LogInformation(
                "인앱결제 처리 완료 — PlayerId: {PlayerId}, PurchaseId: {Id}, ProductId: {ProductId}, AlreadyGranted: {Already}",
                playerId, purchase.Id, request.ProductId, grantResult.AlreadyGranted);

            // Consumable 상품은 트랜잭션 커밋 후 consume 호출 — 람다 외부 변수에 참조 전달
            if (product.ProductType == IapProductType.Consumable)
                purchaseForConsume = purchase;

            return new IapVerifyResult(Ok: true, AlreadyGranted: grantResult.AlreadyGranted, PurchaseId: purchase.Id);
        });

        // (k) 트랜잭션 커밋 완료 후 consume 호출 — 실패해도 유저 응답 불변, retry 워커 위임
        if (purchaseForConsume is not null)
            await ExecuteConsumeAsync(purchaseForConsume, request.ProductId, request.PurchaseToken);

        return result;
    }

    // 트랜잭션 커밋 후 Google Play consume 호출 — 실패해도 유저 응답 불변
    // 영구실패: ConsumedAt 마킹 후 중단. 일시실패: 시도 횟수 증가 + retry 워커 위임
    private async Task ExecuteConsumeAsync(IapPurchase purchase, string productId, string purchaseToken)
    {
        try
        {
            var consumer = _consumerResolver.Resolve(IapStore.Google);
            await consumer.ConsumeAsync(productId, purchaseToken);

            // 성공 — ConsumedAt 기록
            purchase.ConsumedAt = DateTime.UtcNow;
            purchase.UpdatedAt = DateTime.UtcNow;
            await _purchaseRepo.SaveChangesAsync();

            _logger.LogInformation("consume 완료 — PurchaseId: {Id}", purchase.Id);
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
                        dedupKey: $"iap-consume-fail:{purchase.Id}");
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

            await _purchaseRepo.SaveChangesAsync();
        }
    }

    // RewardTable의 항목으로 RewardBundle 구성
    // Weight가 있는 항목은 가중치 확률 추첨, 없으면 전체 고정 지급 (AdRewardService 동일 패턴)
    private async Task<RewardBundle> BuildBundleAsync(int? rewardTableId)
    {
        if (rewardTableId is null)
            return new RewardBundle();

        // ID로 RewardTable + Entries 조회
        var table = await _rewardTableRepo.GetByIdWithEntriesAsync(rewardTableId.Value);
        if (table is null || table.IsDeleted)
            return new RewardBundle();

        var entries = table.Entries.ToList();
        if (entries.Count == 0)
            return new RewardBundle();

        // Weight가 있는 항목이 있으면 확률 추첨, 없으면 전체 고정 지급
        bool hasWeight = entries.Any(e => e.Weight.HasValue);

        if (hasWeight)
        {
            // 가중치 기반 확률 추첨 — 하나의 항목만 선택
            var totalWeight = entries.Sum(e => e.Weight ?? 0);
            if (totalWeight <= 0)
                return new RewardBundle();

            var roll = Random.Shared.Next(totalWeight);
            var cumulative = 0;
            foreach (var entry in entries)
            {
                cumulative += entry.Weight ?? 0;
                if (roll < cumulative)
                {
                    return new RewardBundle(Items: new[]
                    {
                        new RewardItem(entry.ItemId, entry.Count)
                    });
                }
            }
        }
        else
        {
            // 전체 고정 지급 — Weight 없는 모든 항목 지급
            var items = entries
                .Select(e => new RewardItem(e.ItemId, e.Count))
                .ToArray();
            return new RewardBundle(Items: items);
        }

        return new RewardBundle();
    }

    // PostgreSQL UNIQUE 제약 위반 여부 확인
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
           || (ex.InnerException?.GetType().Name == "PostgresException" &&
               (ex.InnerException?.Message.Contains("unique") == true ||
                ex.InnerException?.Message.Contains("duplicate") == true));

    // 로그에 구매 토큰 전체 노출 방지 — 앞 8자만 표시
    private static string MaskToken(string token)
        => token.Length > 8 ? token[..8] + "..." : "***";
}
