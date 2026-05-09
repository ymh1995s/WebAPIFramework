using Framework.Application.Common;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Reward;
using Framework.Domain.Constants;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Shop;

// 인게임 상점 구매 서비스 구현체
// [처리 흐름]
// 1. 상품 존재·활성화 여부 확인 (트랜잭션 밖)
// 2. 가격 재화 보유량 사전 확인 (트랜잭션 밖 — 빠른 실패)
// 3. 일일/총 구매 한도 확인 (RewardGrant 이력 기반)
// 4. 보상 테이블 번들 사전 구성 확인
// 5. 트랜잭션 내부: 재화 잔량 재확인 → 차감 → 감사 로그 → RewardDispatcher 보상 지급
//    - GrantAsync 내부의 UNIQUE 선기록이 멱등성(중복 요청) 차단을 담당
// [동시성] RewardDispatcher가 PlayerItem.xmin 낙관적 동시성 충돌 시 최대 3회 재시도를 담당
public class ShopPurchaseService : IShopPurchaseService
{
    private readonly IShopProductRepository _shopRepo;
    private readonly IPlayerItemRepository _itemRepo;
    private readonly IRewardGrantRepository _grantRepo;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShopPurchaseService> _logger;

    public ShopPurchaseService(
        IShopProductRepository shopRepo,
        IPlayerItemRepository itemRepo,
        IRewardGrantRepository grantRepo,
        IRewardTableRepository rewardTableRepo,
        IRewardDispatcher rewardDispatcher,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<ShopPurchaseService> logger)
    {
        _shopRepo = shopRepo;
        _itemRepo = itemRepo;
        _grantRepo = grantRepo;
        _rewardTableRepo = rewardTableRepo;
        _rewardDispatcher = rewardDispatcher;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 활성 상품 목록 조회 — 클라이언트 상점 화면 표시용
    public async Task<List<ShopProductDto>> GetActiveProductsAsync()
    {
        var products = await _shopRepo.GetActiveListAsync();
        return products.Select(p => new ShopProductDto(
            p.Id,
            p.Name,
            p.Description,
            p.PriceItemId,
            p.PriceItem?.Name ?? $"ItemId:{p.PriceItemId}",
            p.PriceAmount,
            p.RewardTableId,
            p.DailyLimit,
            p.TotalLimit,
            p.IsEnabled,
            p.SortOrder,
            p.CreatedAt,
            p.UpdatedAt
        )).ToList();
    }

    // 상품 구매 처리 — 재화 차감 + 보상 지급 원자 트랜잭션
    // [멱등성] GrantAsync 내부의 RewardGrant 선기록(UNIQUE 위반 catch)이 중복 요청 차단 담당
    // [원자성] IUnitOfWork.ExecuteInTransactionAsync<T>로 차감 + 지급 전체를 단일 트랜잭션으로 묶음
    public async Task<ShopPurchaseResult> BuyAsync(int playerId, int productId, string clientRequestId)
    {
        // 1단계: 상품 존재·활성화 여부 확인 (트랜잭션 밖에서 사전 검증)
        var product = await _shopRepo.GetByIdAsync(productId);
        if (product is null || !product.IsEnabled || product.IsDeleted)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 상품 미존재 또는 비활성화: PlayerId={PlayerId}, ProductId={ProductId}",
                playerId, productId);
            return new ShopPurchaseResult(ShopPurchaseStatus.ProductNotFound);
        }

        // 2단계: 가격 재화 보유량 사전 확인 (트랜잭션 밖 — 빠른 실패)
        var priceItem = await _itemRepo.GetByPlayerAndItemAsync(playerId, product.PriceItemId);
        if (priceItem is null || priceItem.Quantity < product.PriceAmount)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 재화 부족: PlayerId={PlayerId}, ProductId={ProductId}, 필요={Need}, 보유={Have}",
                playerId, productId, product.PriceAmount, priceItem?.Quantity ?? 0);
            return new ShopPurchaseResult(ShopPurchaseStatus.NotEnoughCurrency);
        }

        // 3단계: 구매 한도 확인 — RewardGrant 이력에서 SourceKey prefix로 카운트
        // SourceKey 형식: "shop:{playerId}:{productId}:{clientRequestId}"
        // prefix "shop:{playerId}:{productId}:"로 이 상품의 구매 건수 집계
        var sourceKeyPrefix = $"shop:{playerId}:{productId}:";

        if (product.TotalLimit > 0)
        {
            // 총 구매 한도 확인 — DateTime.MinValue를 utcDayStart로 전달하면 전체 기간 카운트
            var totalCount = await _grantRepo.CountTodayAsync(
                playerId, RewardSourceType.ShopPurchase, sourceKeyPrefix, DateTime.MinValue);
            if (totalCount >= product.TotalLimit)
            {
                _logger.LogWarning(
                    "상점 구매 실패 — 총 구매 한도 초과: PlayerId={PlayerId}, ProductId={ProductId}, 한도={Limit}, 누적={Count}",
                    playerId, productId, product.TotalLimit, totalCount);
                return new ShopPurchaseResult(ShopPurchaseStatus.TotalLimitExceeded);
            }
        }

        if (product.DailyLimit > 0)
        {
            // 일일 구매 한도 확인 — UTC 기준 오늘 00:00 이후 카운트
            var utcDayStart = DateTime.UtcNow.Date;
            var todayCount = await _grantRepo.CountTodayAsync(
                playerId, RewardSourceType.ShopPurchase, sourceKeyPrefix, utcDayStart);
            if (todayCount >= product.DailyLimit)
            {
                _logger.LogWarning(
                    "상점 구매 실패 — 일일 구매 한도 초과: PlayerId={PlayerId}, ProductId={ProductId}, 한도={Limit}, 오늘={Count}",
                    playerId, productId, product.DailyLimit, todayCount);
                return new ShopPurchaseResult(ShopPurchaseStatus.DailyLimitExceeded);
            }
        }

        // 4단계: 보상 테이블 번들 사전 확인 (트랜잭션 밖에서 빠른 실패)
        var rewardTable = await _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId);
        if (rewardTable is null || rewardTable.Entries.Count == 0)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 보상 테이블 비어있음: PlayerId={PlayerId}, ProductId={ProductId}, RewardTableId={TableId}",
                playerId, productId, product.RewardTableId);
            return new ShopPurchaseResult(ShopPurchaseStatus.RewardTableEmpty);
        }

        // 보상 번들 구성 — RewardTableEntry 목록을 RewardItem 리스트로 변환
        var rewardItems = rewardTable.Entries
            .Select(e => new RewardItem(e.ItemId, e.Count))
            .ToList();
        var bundle = new RewardBundle(Items: rewardItems);

        // 5단계: 트랜잭션 내부에서 재화 차감 + 보상 지급 원자 처리
        // ExecuteInTransactionAsync<T>를 사용하여 결과를 직접 반환 — 외부 변수 없이 명확한 흐름 유지
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 5-1: 재화 최신 잔량 재확인 — 사전 검증 이후 동시 소비로 잔량이 부족해진 경우(TOCTOU) 방지
            var freshPriceItem = await _itemRepo.GetByPlayerAndItemAsync(playerId, product.PriceItemId);
            if (freshPriceItem is null || freshPriceItem.Quantity < product.PriceAmount)
            {
                return new ShopPurchaseResult(ShopPurchaseStatus.NotEnoughCurrency);
            }

            // 5-2: 재화 차감 기록 (EF ChangeTracker에만 반영 — SaveChangesAsync 이전)
            var quantityBefore = freshPriceItem.Quantity;
            freshPriceItem.Quantity -= product.PriceAmount;

            // 5-3: 보상 지급 — RewardDispatcher가 UNIQUE 선기록 + 지급 + 멱등성을 한 번에 처리
            // 이미 외부 트랜잭션이 활성화되어 있으므로 GrantAsync는 참여자로 합류
            var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
                PlayerId: playerId,
                SourceType: RewardSourceType.ShopPurchase,
                SourceKey: SourceKeys.ShopPurchase(playerId, productId, clientRequestId),
                Bundle: bundle,
                MailTitle: $"상점 구매 보상: {product.Name}",
                MailBody: $"'{product.Name}' 상품 구매 보상입니다.",
                Mode: DispatchMode.Direct
            ));

            if (grantResult.AlreadyGranted)
            {
                // 동일 clientRequestId로 이미 처리된 요청 — 재화 차감 복원 후 Duplicate 반환
                freshPriceItem.Quantity = quantityBefore;
                _logger.LogWarning(
                    "상점 구매 중복 요청 — PlayerId={PlayerId}, ProductId={ProductId}, ClientRequestId={Req}",
                    playerId, productId, clientRequestId);
                return new ShopPurchaseResult(ShopPurchaseStatus.Duplicate);
            }

            if (!grantResult.Success)
            {
                // 보상 지급 실패 — 재화 차감 복원 후 RewardGrantFailed 반환
                freshPriceItem.Quantity = quantityBefore;
                _logger.LogError(
                    "상점 구매 보상 지급 실패 — PlayerId={PlayerId}, ProductId={ProductId}, 사유={Msg}",
                    playerId, productId, grantResult.Message);
                return new ShopPurchaseResult(ShopPurchaseStatus.RewardGrantFailed);
            }

            // 5-4: 보상 지급 성공 확인 후 감사 로그 기록 — 실제 차감이 확정된 시점에만 기록
            await _auditLogService.RecordAsync(
                playerId,
                product.PriceItemId,
                reason: AuditLogReasons.ShopPurchase,
                changeAmount: -product.PriceAmount,
                balanceBefore: quantityBefore,
                balanceAfter: freshPriceItem.Quantity);

            _logger.LogInformation(
                "상점 구매 완료 — PlayerId={PlayerId}, ProductId={ProductId}, 상품={Name}",
                playerId, productId, product.Name);

            return new ShopPurchaseResult(ShopPurchaseStatus.Success);
        });
    }
}
