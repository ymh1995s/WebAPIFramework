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
            p.UpdatedAt,
            p.MaxPerCall
        )).ToList();
    }

    // 상품 구매 처리 — N개 구매, 재화 차감 + 보상 지급 원자 트랜잭션
    // [멱등성] GrantAsync 내부의 RewardGrant 선기록(UNIQUE 위반 catch)이 중복 요청 차단 담당
    // [원자성] IUnitOfWork.ExecuteInTransactionAsync<T>로 차감 + 지급 전체를 단일 트랜잭션으로 묶음
    // [N개 구매] quantity > 1 시 가격·보상·감사 로그 모두 quantity 배율로 처리
    public async Task<ShopPurchaseResult> BuyAsync(int playerId, int productId, string clientRequestId, int quantity = 1)
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

        // 1-1단계: MaxPerCall 검증 — 설정된 경우 요청 수량이 1회 한도 초과 여부 확인
        if (product.MaxPerCall.HasValue && quantity > product.MaxPerCall.Value)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 1회 최대 수량 초과: PlayerId={PlayerId}, ProductId={ProductId}, MaxPerCall={Max}, 요청={Qty}",
                playerId, productId, product.MaxPerCall.Value, quantity);
            return new ShopPurchaseResult(ShopPurchaseStatus.MaxPerCallExceeded);
        }

        // 2단계: N개 총 가격 계산 (long으로 오버플로우 방어)
        var totalPrice = (long)product.PriceAmount * quantity;

        // 총 가격이 int 범위를 초과하면 재화 부족으로 처리 (현실적으로 발생 불가하나 안전장치)
        if (totalPrice > int.MaxValue)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 총 가격 오버플로우: PlayerId={PlayerId}, ProductId={ProductId}, 총가격={Total}",
                playerId, productId, totalPrice);
            return new ShopPurchaseResult(ShopPurchaseStatus.NotEnoughCurrency);
        }

        var totalPriceInt = (int)totalPrice;

        // 2-1단계: 가격 재화 보유량 사전 확인 (트랜잭션 밖 — 빠른 실패)
        var priceItem = await _itemRepo.GetByPlayerAndItemAsync(playerId, product.PriceItemId);
        if (priceItem is null || priceItem.Quantity < totalPriceInt)
        {
            _logger.LogWarning(
                "상점 구매 실패 — 재화 부족: PlayerId={PlayerId}, ProductId={ProductId}, 필요={Need}, 보유={Have}",
                playerId, productId, totalPriceInt, priceItem?.Quantity ?? 0);
            return new ShopPurchaseResult(ShopPurchaseStatus.NotEnoughCurrency);
        }

        // 3단계: 구매 한도 확인 — RewardGrant 이력에서 SourceKey prefix로 수량 합계 집계
        // SourceKey 형식: "shop:{playerId}:{productId}:{clientRequestId}"
        // prefix "shop:{playerId}:{productId}:"로 이 상품의 누적 구매 수량 집계
        var sourceKeyPrefix = $"shop:{playerId}:{productId}:";

        if (product.TotalLimit > 0)
        {
            // 총 구매 한도 확인 — DateTime.MinValue를 utcDayStart로 전달하면 전체 기간 수량 합계
            var totalSum = await _grantRepo.SumQuantityAsync(
                playerId, RewardSourceType.ShopPurchase, sourceKeyPrefix, DateTime.MinValue);
            var totalRemaining = product.TotalLimit - totalSum;
            if (quantity > totalRemaining)
            {
                _logger.LogWarning(
                    "상점 구매 실패 — 총 구매 한도 초과: PlayerId={PlayerId}, ProductId={ProductId}, 한도={Limit}, 누적={Sum}, 요청={Qty}, 잔여={Remaining}",
                    playerId, productId, product.TotalLimit, totalSum, quantity, totalRemaining);
                return new ShopPurchaseResult(ShopPurchaseStatus.LimitWouldExceed, RemainingQuantity: Math.Max(0, totalRemaining));
            }
        }

        if (product.DailyLimit > 0)
        {
            // 일일 구매 한도 확인 — UTC 기준 오늘 00:00 이후 수량 합계
            var utcDayStart = DateTime.UtcNow.Date;
            var todaySum = await _grantRepo.SumQuantityAsync(
                playerId, RewardSourceType.ShopPurchase, sourceKeyPrefix, utcDayStart);
            var dailyRemaining = product.DailyLimit - todaySum;
            if (quantity > dailyRemaining)
            {
                _logger.LogWarning(
                    "상점 구매 실패 — 일일 구매 한도 초과: PlayerId={PlayerId}, ProductId={ProductId}, 한도={Limit}, 오늘={Sum}, 요청={Qty}, 잔여={Remaining}",
                    playerId, productId, product.DailyLimit, todaySum, quantity, dailyRemaining);
                return new ShopPurchaseResult(ShopPurchaseStatus.LimitWouldExceed, RemainingQuantity: Math.Max(0, dailyRemaining));
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

        // 보상 번들 구성 — quantity 배율 적용 (각 항목 수량 × quantity)
        // checked 블록으로 int 오버플로우 감지 (매우 큰 quantity + Count 조합 방어)
        var rewardItems = rewardTable.Entries
            .Select(e =>
            {
                var rewardQty = checked(e.Count * quantity);
                return new RewardItem(e.ItemId, rewardQty);
            })
            .ToList();
        var bundle = new RewardBundle(Items: rewardItems);

        // 5단계: 트랜잭션 내부에서 재화 차감 + 보상 지급 원자 처리
        // ExecuteInTransactionAsync<T>를 사용하여 결과를 직접 반환 — 외부 변수 없이 명확한 흐름 유지
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 5-1: 재화 최신 잔량 재확인 — 사전 검증 이후 동시 소비로 잔량이 부족해진 경우(TOCTOU) 방지
            var freshPriceItem = await _itemRepo.GetByPlayerAndItemAsync(playerId, product.PriceItemId);
            if (freshPriceItem is null || freshPriceItem.Quantity < totalPriceInt)
            {
                return new ShopPurchaseResult(ShopPurchaseStatus.NotEnoughCurrency);
            }

            // 5-2: 재화 차감 기록 (EF ChangeTracker에만 반영 — SaveChangesAsync 이전)
            var quantityBefore = freshPriceItem.Quantity;
            freshPriceItem.Quantity -= totalPriceInt;

            // 5-3: 보상 지급 — RewardDispatcher가 UNIQUE 선기록 + 지급 + 멱등성을 한 번에 처리
            // 이미 외부 트랜잭션이 활성화되어 있으므로 GrantAsync는 참여자로 합류
            // PurchasedQuantity = quantity로 전달 — DailyLimit/TotalLimit SUM 집계용
            var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
                PlayerId: playerId,
                SourceType: RewardSourceType.ShopPurchase,
                SourceKey: SourceKeys.ShopPurchase(playerId, productId, clientRequestId),
                Bundle: bundle,
                MailTitle: $"상점 구매 보상: {product.Name}",
                MailBody: $"'{product.Name}' 상품 {quantity}개 구매 보상입니다.",
                Mode: DispatchMode.Direct,
                PurchasedQuantity: quantity
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
            // changeAmount는 총 차감량 (단가 × 수량)의 음수값
            await _auditLogService.RecordAsync(
                playerId,
                product.PriceItemId,
                reason: AuditLogReasons.ShopPurchase,
                changeAmount: -totalPriceInt,
                balanceBefore: quantityBefore,
                balanceAfter: freshPriceItem.Quantity);

            _logger.LogInformation(
                "상점 구매 완료 — PlayerId={PlayerId}, ProductId={ProductId}, 상품={Name}, 수량={Qty}",
                playerId, productId, product.Name, quantity);

            return new ShopPurchaseResult(ShopPurchaseStatus.Success);
        });
    }
}
