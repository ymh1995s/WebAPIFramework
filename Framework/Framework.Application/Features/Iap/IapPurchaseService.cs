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
public class IapPurchaseService : IIapPurchaseService
{
    private readonly IIapProductRepository _productRepo;
    private readonly IIapPurchaseRepository _purchaseRepo;
    private readonly IIapStoreVerifierResolver _verifierResolver;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IapPurchaseService> _logger;

    public IapPurchaseService(
        IIapProductRepository productRepo,
        IIapPurchaseRepository purchaseRepo,
        IIapStoreVerifierResolver verifierResolver,
        IRewardDispatcher rewardDispatcher,
        IRewardTableRepository rewardTableRepo,
        IUnitOfWork unitOfWork,
        ILogger<IapPurchaseService> logger)
    {
        _productRepo = productRepo;
        _purchaseRepo = purchaseRepo;
        _verifierResolver = verifierResolver;
        _rewardDispatcher = rewardDispatcher;
        _rewardTableRepo = rewardTableRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 구매 영수증 검증 후 보상 지급 — 멱등성 보장 (동일 토큰 재요청 처리)
    public async Task<IapVerifyResult> VerifyAndGrantAsync(
        int playerId, IapVerifyRequest request, string? clientIp)
    {
        // (a) 활성 상품 조회 — 스토어에 등록된 유효한 상품인지 확인
        // Google 스토어 고정 (Phase 3 범위), 향후 request.Store로 분기 예정
        var product = await _productRepo.FindActiveAsync(IapStore.Google, request.ProductId);
        if (product is null)
        {
            _logger.LogWarning(
                "인앱결제 상품 없음 — Store: Google, ProductId: {ProductId}",
                request.ProductId);
            throw new IapProductNotFoundException(IapStore.Google, request.ProductId);
        }

        // (b) 동일 구매 토큰 중복 처리 체크 — 클라이언트 재시도 대응
        var existingPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);
        if (existingPurchase is not null)
        {
            // [보안] 소유자 검사를 Status 검사보다 먼저 수행 — 타 플레이어 토큰으로 AlreadyGranted 응답 유출 방지
            if (existingPurchase.PlayerId != playerId)
            {
                _logger.LogWarning(
                    "구매 토큰 소유자 불일치 — 요청 PlayerId: {RequestPlayer}, 토큰 소유자: {TokenOwner}",
                    playerId, existingPurchase.PlayerId);
                throw new IapTokenOwnershipMismatchException(IapStore.Google, request.PurchaseToken);
            }

            // 이미 보상 지급 완료된 구매 → 멱등 응답 반환
            if (existingPurchase.Status == IapPurchaseStatus.Granted)
            {
                _logger.LogInformation(
                    "중복 구매 요청 (이미 지급됨) — PlayerId: {PlayerId}, PurchaseToken: {Token}",
                    playerId, MaskToken(request.PurchaseToken));
                return new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: existingPurchase.Id);
            }
        }

        // (c) 트랜잭션 시작 — Pending 저장부터 Granted까지 원자적으로 처리
        await _unitOfWork.BeginTransactionAsync();

        // (d) 구매 이력 Pending 상태로 먼저 저장 — UNIQUE(Store, PurchaseToken) 제약으로 중복 방지
        var purchase = new IapPurchase
        {
            PlayerId = playerId,
            Store = IapStore.Google,
            ProductId = request.ProductId,
            PurchaseToken = request.PurchaseToken,
            OrderId = request.OrderId,
            Status = IapPurchaseStatus.Pending,
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
            // UNIQUE 제약 위반 — 동시 요청 경쟁 조건에서 먼저 저장된 레코드가 있을 때
            // 현재 트랜잭션 롤백 후, 기존 레코드 상태 확인
            await _unitOfWork.RollbackAsync();

            var concurrentPurchase = await _purchaseRepo.FindByTokenAsync(IapStore.Google, request.PurchaseToken);
            if (concurrentPurchase?.Status == IapPurchaseStatus.Granted)
            {
                return new IapVerifyResult(Ok: true, AlreadyGranted: true, PurchaseId: concurrentPurchase.Id);
            }

            // 동시 요청이 아직 처리 중이거나 실패 상태인 경우 — 오류 반환
            _logger.LogWarning(
                "중복 구매 토큰 저장 시도 충돌 — PlayerId: {PlayerId}, Token: {Token}",
                playerId, MaskToken(request.PurchaseToken));
            throw new IapReceiptInvalidException(IapStore.Google, "동시 처리 중인 구매입니다. 잠시 후 다시 시도해주세요.");
        }

        // (e) 외부 스토어 검증 API 호출
        IapReceiptVerified verified;
        try
        {
            var verifier = _verifierResolver.Resolve(IapStore.Google);
            verified = await verifier.VerifyAsync(request.ProductId, request.PurchaseToken);
        }
        catch (Exception ex)
        {
            // 검증 실패 — Status=Failed로 기록 후 트랜잭션 롤백
            purchase.Status = IapPurchaseStatus.Failed;
            purchase.FailureReason = ex.Message;
            purchase.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _purchaseRepo.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "검증 실패 상태 저장 중 오류 — PurchaseId: {Id}", purchase.Id);
            }

            await _unitOfWork.RollbackAsync();

            _logger.LogWarning(
                ex,
                "Google Play 검증 실패 — PlayerId: {PlayerId}, ProductId: {ProductId}",
                playerId, request.ProductId);
            throw;
        }

        // (f) 영수증 검증 성공 — Verified 상태 및 원본 영수증 저장
        purchase.Status = IapPurchaseStatus.Verified;
        purchase.RawReceipt = verified.RawReceiptJson;
        purchase.PurchaseTimeUtc = verified.PurchaseTimeUtc;
        purchase.VerifiedAt = DateTime.UtcNow;
        purchase.UpdatedAt = DateTime.UtcNow;

        // (g) 보상 번들 구성 — 상품에 연결된 RewardTable 기반
        var bundle = await BuildBundleAsync(product.RewardTableId);

        // (h) RewardDispatcher를 통해 보상 지급
        // SourceKey 형식: "google:{purchaseToken}" — 스토어 + 토큰 조합으로 멱등성 보장
        var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.Purchase,
            SourceKey: $"google:{request.PurchaseToken}",
            Bundle: bundle,
            MailTitle: "인앱결제 보상",
            MailBody: $"'{product.Description}' 상품 구매에 감사드립니다. 보상을 수령해 주세요.",
            Mode: DispatchMode.Direct
        ));

        if (!grantResult.Success && !grantResult.AlreadyGranted)
        {
            // 보상 지급 실패 — 롤백 후 예외 발생
            await _unitOfWork.RollbackAsync();
            _logger.LogError(
                "보상 지급 실패 — PlayerId: {PlayerId}, PurchaseId: {Id}, 이유: {Reason}",
                playerId, purchase.Id, grantResult.Message);
            throw new InvalidOperationException($"보상 지급에 실패했습니다: {grantResult.Message}");
        }

        // (i) 최종 상태 Granted로 갱신 — 보상 테이블 스냅샷 및 지급 시각 기록
        purchase.Status = IapPurchaseStatus.Granted;
        purchase.RewardTableIdSnapshot = product.RewardTableId;
        purchase.GrantedAt = DateTime.UtcNow;
        purchase.UpdatedAt = DateTime.UtcNow;

        await _purchaseRepo.SaveChangesAsync();

        // (j) 트랜잭션 커밋
        await _unitOfWork.CommitAsync();

        _logger.LogInformation(
            "인앱결제 처리 완료 — PlayerId: {PlayerId}, PurchaseId: {Id}, ProductId: {ProductId}, AlreadyGranted: {Already}",
            playerId, purchase.Id, request.ProductId, grantResult.AlreadyGranted);

        // (k) Consumable 상품은 Google consume() 호출 (향후 확장점 — 현재는 로그만)
        // 실패해도 DB는 이미 Granted이므로 예외 무시, 향후 재시도 큐로 처리 예정
        if (product.ProductType == IapProductType.Consumable)
        {
            try
            {
                // TODO: Google Play consumePurchase API 호출 구현 (향후 Phase 4)
                // await ConsumeGooglePurchaseAsync(request.ProductId, request.PurchaseToken);
                _logger.LogDebug(
                    "Consumable 상품 consume 호출 예정 — PurchaseId: {Id}, ProductId: {ProductId}",
                    purchase.Id, request.ProductId);
            }
            catch (Exception consumeEx)
            {
                // consume 실패는 로그만 남기고 무시 (DB는 이미 Granted 완료)
                _logger.LogWarning(
                    consumeEx,
                    "Google consume 호출 실패 (무시) — PurchaseId: {Id}", purchase.Id);
            }
        }

        return new IapVerifyResult(Ok: true, AlreadyGranted: grantResult.AlreadyGranted, PurchaseId: purchase.Id);
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
