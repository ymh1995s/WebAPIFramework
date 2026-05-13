using Framework.Application.Common;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Quest;
using Framework.Application.Features.Reward;
using Framework.Application.Features.Shop;
using Framework.Domain.Constants;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.Unit.Shop;

// ShopPurchaseService 단위 테스트 — 인게임 상점 구매 흐름 전체 검증
// [커버리지 범위] 정상 구매 / 중복 / 보상 실패 / 상품 미존재 / 재화 부족 / 한도 초과 / 보상 테이블 없음 / TOCTOU
public class ShopPurchaseServiceTests
{
    // ── 공통 대리자 ──────────────────────────────────────────────────────────

    private readonly IShopProductRepository   _shopRepo;
    private readonly IPlayerItemRepository    _itemRepo;
    private readonly IRewardGrantRepository   _grantRepo;
    private readonly IRewardTableRepository   _rewardTableRepo;
    private readonly IRewardDispatcher        _rewardDispatcher;
    private readonly IAuditLogService         _auditLogService;
    private readonly IQuestProgressService    _questProgressService;
    private readonly IUnitOfWork              _uow;
    private readonly ShopPurchaseService      _sut;

    public ShopPurchaseServiceTests()
    {
        _shopRepo             = Substitute.For<IShopProductRepository>();
        _itemRepo             = Substitute.For<IPlayerItemRepository>();
        _grantRepo            = Substitute.For<IRewardGrantRepository>();
        _rewardTableRepo      = Substitute.For<IRewardTableRepository>();
        _rewardDispatcher     = Substitute.For<IRewardDispatcher>();
        _auditLogService      = Substitute.For<IAuditLogService>();
        // 퀘스트 진행 서비스 — 상점 구매 시 퀘스트 조건 트리거용 (테스트에서는 동작 불필요)
        _questProgressService = Substitute.For<IQuestProgressService>();

        // IUnitOfWork 패스스루 설정 — Task<ShopPurchaseResult> 오버로드 포함
        _uow = UnitOfWorkSubstitute.CreatePassthrough();
        UnitOfWorkSubstitute.ConfigurePassthrough<ShopPurchaseResult>(_uow);

        _sut = new ShopPurchaseService(
            _shopRepo,
            _itemRepo,
            _grantRepo,
            _rewardTableRepo,
            _rewardDispatcher,
            _auditLogService,
            _questProgressService,
            _uow,
            NullLogger<ShopPurchaseService>.Instance);
    }

    // ── 헬퍼 메서드 ──────────────────────────────────────────────────────────

    // 기본 ShopProduct 인스턴스 생성
    private static ShopProduct MakeProduct(
        int id = 10, int priceItemId = 1, int priceAmount = 100,
        int rewardTableId = 20, int dailyLimit = 0, int totalLimit = 0,
        bool isEnabled = true, bool isDeleted = false, int? maxPerCall = null) =>
        new()
        {
            Id           = id,
            Name         = "테스트 상품",
            PriceItemId  = priceItemId,
            PriceAmount  = priceAmount,
            RewardTableId = rewardTableId,
            DailyLimit   = dailyLimit,
            TotalLimit   = totalLimit,
            IsEnabled    = isEnabled,
            IsDeleted    = isDeleted,
            // 1회 최대 구매 수량 — null이면 무제한
            MaxPerCall   = maxPerCall,
        };

    // 기본 PlayerItem 인스턴스 생성 (인벤토리 행)
    private static PlayerItem MakePlayerItem(int playerId = 1, int itemId = 1, int qty = 1000) =>
        new()
        {
            PlayerId = playerId,
            ItemId   = itemId,
            Quantity = qty,
        };

    // 기본 RewardTable 인스턴스 생성
    private static RewardTable MakeRewardTable(int id = 20, ICollection<RewardTableEntry>? entries = null)
    {
        // entries가 null이면 기본 항목 1개 포함
        entries ??= new List<RewardTableEntry> { new() { ItemId = 99, Count = 5 } };
        return new RewardTable { Id = id, Entries = entries };
    }

    // 정상 구매 시나리오 공통 setup — 모든 사전 조건 만족 상태
    private void SetupSuccessScenario(
        int playerId = 1, int productId = 10, string clientRequestId = "req-001")
    {
        var product     = MakeProduct();
        var priceItem   = MakePlayerItem(playerId, product.PriceItemId, qty: 1000);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(productId).Returns(product);
        // 트랜잭션 내외 두 번 호출 — 첫 번째(사전 확인), 두 번째(TOCTOU 재확인)
        _itemRepo.GetByPlayerAndItemAsync(playerId, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(playerId, product.PriceItemId, qty: 1000));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        // GrantAsync — DirectSuccess 반환
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());
    }

    // ════════════════════════════════════════════════════════════════════════
    // 정상 구매 시나리오 (1~3번)
    // ════════════════════════════════════════════════════════════════════════

    // ── 1. 정상 구매 → AuditLog 1회 기록, 다른 실패 분기 미진입 ─────────────

    [Fact]
    public async Task BuyAsync_Success_RecordsAuditLogOnce()
    {
        // Arrange
        SetupSuccessScenario();

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: Success 반환 + AuditLog 정확히 1회 호출
        Assert.Equal(ShopPurchaseStatus.Success, result.Status);
        await _auditLogService.Received(1).RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    // ── 2. 정상 구매 → AuditLog 인자 정확성 검증 ────────────────────────────
    // playerId, priceItemId, 이유, -priceAmount, balanceBefore, balanceAfter

    [Fact]
    public async Task BuyAsync_Success_AuditLogArgumentsCorrect()
    {
        // Arrange: priceAmount=100, 보유량=500 → 잔량 before=500, after=400
        const int playerId    = 1;
        const int priceItemId = 1;
        const int priceAmount = 100;
        const int initialQty  = 500;

        var product     = MakeProduct(priceAmount: priceAmount, priceItemId: priceItemId);
        var priceItem   = MakePlayerItem(playerId, priceItemId, qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(playerId, priceItemId)
                 .Returns(priceItem, MakePlayerItem(playerId, priceItemId, qty: initialQty));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        await _sut.BuyAsync(playerId, 10, "req-001");

        // Assert: RecordAsync 인자가 정확히 일치해야 함
        await _auditLogService.Received(1).RecordAsync(
            playerId,                   // 플레이어 ID
            priceItemId,                // 차감 아이템 ID
            AuditLogReasons.ShopPurchase,
            -priceAmount,              // 음수 차감량
            initialQty,                // 차감 전 잔량
            initialQty - priceAmount); // 차감 후 잔량
    }

    // ── 3. Duplicate 반환 → AuditLog 미호출 + 재화 수량 복원 ────────────────

    [Fact]
    public async Task BuyAsync_Duplicate_DoesNotRecordAuditLogAndRestoresQuantity()
    {
        // Arrange: GrantAsync가 Duplicate 반환
        const int initialQty = 1000;
        var product     = MakeProduct();
        var priceItem   = MakePlayerItem(1, product.PriceItemId, qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(1, product.PriceItemId, qty: initialQty));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        // 중복 요청 시뮬레이션
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.Duplicate());

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-dup");

        // Assert: Duplicate 반환, AuditLog 미호출, 수량 복원
        Assert.Equal(ShopPurchaseStatus.Duplicate, result.Status);
        await _auditLogService.DidNotReceive().RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        // Duplicate 경로에서 Quantity가 복원되므로 originalQty와 동일해야 함
        Assert.Equal(initialQty, priceItem.Quantity);
    }

    // ── 4. GrantAsync 실패 → AuditLog 미호출 + 재화 수량 복원 ──────────────

    [Fact]
    public async Task BuyAsync_RewardGrantFailed_DoesNotRecordAuditLogAndRestoresQuantity()
    {
        // Arrange: GrantAsync가 Fail 반환
        const int initialQty = 1000;
        var product     = MakeProduct();
        var priceItem   = MakePlayerItem(1, product.PriceItemId, qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(1, product.PriceItemId, qty: initialQty));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        // 보상 지급 실패 시뮬레이션
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.Fail("내부 오류"));

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-fail");

        // Assert: RewardGrantFailed 반환, AuditLog 미호출, 수량 복원
        Assert.Equal(ShopPurchaseStatus.RewardGrantFailed, result.Status);
        await _auditLogService.DidNotReceive().RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        Assert.Equal(initialQty, priceItem.Quantity);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 사전 검증 실패 시나리오 (5~9번 — 트랜잭션 진입 없음)
    // ════════════════════════════════════════════════════════════════════════

    // ── 5. 상품 미존재/삭제/비활성화 → ProductNotFound, 사이드이펙트 없음 ────

    [Theory]
    [InlineData(false, false, false)] // null 상품
    [InlineData(true, true, false)]   // IsDeleted=true
    [InlineData(true, false, false)]  // IsEnabled=false
    public async Task BuyAsync_ProductNotFound_ReturnsProductNotFoundWithoutSideEffects(
        bool productExists, bool isDeleted, bool isEnabled)
    {
        // Arrange: null 또는 비활성/삭제 상품
        ShopProduct? product = productExists
            ? MakeProduct(isDeleted: isDeleted, isEnabled: isEnabled)
            : null;
        _shopRepo.GetByIdAsync(10).Returns(product);

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: ProductNotFound, 트랜잭션 미진입
        Assert.Equal(ShopPurchaseStatus.ProductNotFound, result.Status);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
        await _auditLogService.DidNotReceive().RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    // ── 6. 재화 부족 → NotEnoughCurrency, 트랜잭션 진입 없음 ─────────────

    [Fact]
    public async Task BuyAsync_NotEnoughCurrency_ReturnsBeforeTransaction()
    {
        // Arrange: 재화 보유량(50) < 가격(100)
        var product = MakeProduct(priceAmount: 100);
        _shopRepo.GetByIdAsync(10).Returns(product);
        // 재화 부족 상황
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(MakePlayerItem(qty: 50));

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: NotEnoughCurrency, 트랜잭션 미진입
        Assert.Equal(ShopPurchaseStatus.NotEnoughCurrency, result.Status);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
    }

    // ── 7. 일일 구매 한도 초과(수량 초과) → LimitWouldExceed + RemainingQuantity 반환, 트랜잭션 미진입 ─────────

    [Fact]
    public async Task BuyAsync_DailyLimitExceeded_ReturnsBeforeTransaction()
    {
        // Arrange: DailyLimit=3, 오늘 이미 3 수량 구매 → 잔여 0
        var product = MakeProduct(dailyLimit: 3);
        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(MakePlayerItem(qty: 1000));

        // 오늘 구매 수량 합계 = 한도와 동일 → 잔여 0
        var sourceKeyPrefix = $"shop:1:10:";
        _grantRepo.SumQuantityAsync(1, RewardSourceType.ShopPurchase, sourceKeyPrefix, Arg.Any<DateTime>())
                  .Returns(3);

        // Act: 기본 수량 1 요청 → 잔여 0이므로 초과
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: LimitWouldExceed 반환, RemainingQuantity=0, 트랜잭션 미진입
        Assert.Equal(ShopPurchaseStatus.LimitWouldExceed, result.Status);
        Assert.Equal(0, result.RemainingQuantity);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
    }

    // ── 8. 총 구매 한도 초과(수량 초과) → LimitWouldExceed + RemainingQuantity 반환, 트랜잭션 미진입 ───────────

    [Fact]
    public async Task BuyAsync_TotalLimitExceeded_ReturnsBeforeTransaction()
    {
        // Arrange: TotalLimit=10, 전체 기간 이미 10 수량 구매 → 잔여 0
        var product = MakeProduct(totalLimit: 10);
        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(MakePlayerItem(qty: 1000));

        // DateTime.MinValue를 utcDayStart로 전달하면 전체 기간 수량 합계
        var sourceKeyPrefix = $"shop:1:10:";
        _grantRepo.SumQuantityAsync(1, RewardSourceType.ShopPurchase, sourceKeyPrefix, DateTime.MinValue)
                  .Returns(10);

        // Act: 기본 수량 1 요청 → 잔여 0이므로 초과
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: LimitWouldExceed 반환, RemainingQuantity=0, 트랜잭션 미진입
        Assert.Equal(ShopPurchaseStatus.LimitWouldExceed, result.Status);
        Assert.Equal(0, result.RemainingQuantity);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
    }

    // ── 9. 보상 테이블 비어있음 → RewardTableEmpty, 트랜잭션 미진입 ──────────

    [Theory]
    [InlineData(false)] // RewardTable null
    [InlineData(true)]  // Entries 빈 목록
    public async Task BuyAsync_RewardTableEmpty_ReturnsBeforeTransaction(bool tableExists)
    {
        // Arrange: 보상 테이블 없음 또는 항목 없음
        var product = MakeProduct();
        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(MakePlayerItem(qty: 1000));

        RewardTable? rewardTable = tableExists
            ? MakeRewardTable(entries: new List<RewardTableEntry>()) // 빈 항목
            : null;
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-001");

        // Assert: RewardTableEmpty 반환, 트랜잭션 미진입
        Assert.Equal(ShopPurchaseStatus.RewardTableEmpty, result.Status);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
    }

    // ════════════════════════════════════════════════════════════════════════
    // TOCTOU 및 SourceKey 검증 (10~11번)
    // ════════════════════════════════════════════════════════════════════════

    // ── 10. 트랜잭션 내부 2차 확인에서 재화 부족 → NotEnoughCurrency ──────────
    // 사전 확인 통과 후 동시 소비로 잔량 감소 시나리오 (TOCTOU)

    [Fact]
    public async Task BuyAsync_TocTouFailureInsideTransaction_ReturnsNotEnoughCurrency()
    {
        // Arrange: 1차 호출(사전 확인) → 충분, 2차 호출(트랜잭션 내 재확인) → 부족
        var product     = MakeProduct(priceAmount: 100);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);

        // 첫 번째 호출: 재화 충분 (사전 검사 통과), 두 번째 호출: 재화 부족 (트랜잭션 내)
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(
                     MakePlayerItem(qty: 200), // 1차: 충분
                     MakePlayerItem(qty: 50)   // 2차(트랜잭션 내): 부족
                 );

        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-toctou");

        // Assert: TOCTOU로 NotEnoughCurrency 반환
        Assert.Equal(ShopPurchaseStatus.NotEnoughCurrency, result.Status);
        // AuditLog는 기록되지 않아야 함
        await _auditLogService.DidNotReceive().RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    // ── 11. 정상 구매 → GrantAsync에 올바른 SourceKey/SourceType 전달 검증 ───

    [Fact]
    public async Task BuyAsync_Success_PassesShopPurchaseSourceKey()
    {
        // Arrange: playerId=1, productId=10, clientRequestId="req-abc"
        const int    playerId       = 1;
        const int    productId      = 10;
        const string clientReqId   = "req-abc";

        SetupSuccessScenario(playerId, productId, clientReqId);

        // GrantAsync 호출 시 전달된 요청 캡처
        GrantRewardRequest? captured = null;
        _rewardDispatcher.GrantAsync(Arg.Do<GrantRewardRequest>(r => captured = r))
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        await _sut.BuyAsync(playerId, productId, clientReqId);

        // Assert: SourceType과 SourceKey 검증
        Assert.NotNull(captured);
        Assert.Equal(RewardSourceType.ShopPurchase, captured!.SourceType);
        Assert.Equal(SourceKeys.ShopPurchase(playerId, productId, clientReqId), captured.SourceKey);
    }

    // ════════════════════════════════════════════════════════════════════════
    // N개 구매 시나리오 (12~20번)
    // ════════════════════════════════════════════════════════════════════════

    // ── 12. 수량 5개 구매 → 재화 5배 차감 ───────────────────────────────────

    [Fact]
    public async Task BuyAsync_Quantity5_DeductsPriceTimes5()
    {
        // Arrange: priceAmount=100, 보유량=1000, 수량=5 → 총 차감=500
        const int quantity = 5;
        const int priceAmount = 100;
        const int initialQty = 1000;

        var product   = MakeProduct(priceAmount: priceAmount);
        var priceItem = MakePlayerItem(qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        // 두 번 모두 동일 인스턴스 반환 → 트랜잭션 내 freshPriceItem 차감이 priceItem에 반영됨
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, priceItem);
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-q5", quantity);

        // Assert: 성공 + 재화 5배 차감
        Assert.Equal(ShopPurchaseStatus.Success, result.Status);
        Assert.Equal(initialQty - priceAmount * quantity, priceItem.Quantity);
    }

    // ── 13. 수량 5개 구매 → 보상 번들 수량 5배 ──────────────────────────────

    [Fact]
    public async Task BuyAsync_Quantity5_RewardBundleEntriesMultiplied()
    {
        // Arrange: 보상 테이블 1개 항목(ItemId=99, Count=5) + 구매 수량 5 → 번들 수량=25
        const int quantity = 5;
        const int rewardCount = 5;  // RewardTableEntry.Count
        const int expectedBundleQty = rewardCount * quantity;  // 25

        var product = MakeProduct();
        var priceItem = MakePlayerItem(qty: 1000);
        var rewardTable = MakeRewardTable(entries: new List<RewardTableEntry>
        {
            new() { ItemId = 99, Count = rewardCount }
        });

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: 1000));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);

        // GrantAsync 캡처 — 번들 수량 검증용
        GrantRewardRequest? captured = null;
        _rewardDispatcher.GrantAsync(Arg.Do<GrantRewardRequest>(r => captured = r))
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        await _sut.BuyAsync(1, 10, "req-bundle", quantity);

        // Assert: 번들 내 ItemId=99의 수량이 5×5=25
        Assert.NotNull(captured);
        var bundleItem = captured!.Bundle.Items!.First(i => i.ItemId == 99);
        Assert.Equal(expectedBundleQty, bundleItem.Quantity);
    }

    // ── 14. 수량 5개 구매 → 감사 로그 총 차감량이 -500 ──────────────────────

    [Fact]
    public async Task BuyAsync_Quantity5_AuditLogChangeAmountNegativeTimes5()
    {
        // Arrange: priceAmount=100, quantity=5 → changeAmount=-500
        const int quantity    = 5;
        const int priceAmount = 100;
        const int initialQty  = 1000;

        var product   = MakeProduct(priceAmount: priceAmount);
        var priceItem = MakePlayerItem(qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: initialQty));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        await _sut.BuyAsync(1, 10, "req-audit", quantity);

        // Assert: changeAmount = -(priceAmount * quantity) = -500
        await _auditLogService.Received(1).RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            -(priceAmount * quantity),  // -500
            initialQty,
            initialQty - priceAmount * quantity);
    }

    // ── 15. 수량 요청이 일일 잔여 한도를 초과 → LimitWouldExceed + RemainingQuantity ──

    [Fact]
    public async Task BuyAsync_QuantityExceedsDailyRemainder_LimitWouldExceed_WithRemainingQuantity()
    {
        // Arrange: DailyLimit=10, 오늘 합산=8, 잔여=2, 요청=5 → 초과
        const int dailyLimit = 10;
        const int sumToday   = 8;
        const int requestQty = 5;

        var product = MakeProduct(dailyLimit: dailyLimit);
        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId).Returns(MakePlayerItem(qty: 1000));

        var sourceKeyPrefix = $"shop:1:10:";
        _grantRepo.SumQuantityAsync(1, RewardSourceType.ShopPurchase, sourceKeyPrefix, Arg.Any<DateTime>())
                  .Returns(sumToday);

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-exceed", requestQty);

        // Assert: LimitWouldExceed + 잔여=2
        Assert.Equal(ShopPurchaseStatus.LimitWouldExceed, result.Status);
        Assert.Equal(dailyLimit - sumToday, result.RemainingQuantity);
    }

    // ── 16. 수량 요청이 일일 잔여 한도와 정확히 일치 → 성공 ──────────────────

    [Fact]
    public async Task BuyAsync_QuantityEqualsDailyRemainder_Succeeds()
    {
        // Arrange: DailyLimit=10, 오늘 합산=7, 잔여=3, 요청=3 → 정확히 일치 → 성공
        const int dailyLimit = 10;
        const int sumToday   = 7;
        const int requestQty = 3;

        var product   = MakeProduct(dailyLimit: dailyLimit, priceAmount: 100);
        var priceItem = MakePlayerItem(qty: 1000);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: 1000));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);

        var sourceKeyPrefix = $"shop:1:10:";
        _grantRepo.SumQuantityAsync(1, RewardSourceType.ShopPurchase, sourceKeyPrefix, Arg.Any<DateTime>())
                  .Returns(sumToday);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-exact", requestQty);

        // Assert: Success (정확히 한도까지 허용)
        Assert.Equal(ShopPurchaseStatus.Success, result.Status);
    }

    // ── 17. MaxPerCall 초과 → MaxPerCallExceeded ──────────────────────────

    [Fact]
    public async Task BuyAsync_QuantityExceedsMaxPerCall_Rejected()
    {
        // Arrange: MaxPerCall=3, 요청 수량=5 → 초과
        var product = MakeProduct(maxPerCall: 3);
        _shopRepo.GetByIdAsync(10).Returns(product);

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-maxpercall", 5);

        // Assert: MaxPerCallExceeded, 이후 로직 미진입
        Assert.Equal(ShopPurchaseStatus.MaxPerCallExceeded, result.Status);
        await _itemRepo.DidNotReceive().GetByPlayerAndItemAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    // ── 18. MaxPerCall=null → 대용량 수량도 허용 ──────────────────────────

    [Fact]
    public async Task BuyAsync_MaxPerCallNull_AllowsLargeQuantity()
    {
        // Arrange: MaxPerCall=null(무제한), 요청 수량=100
        var product   = MakeProduct(maxPerCall: null, priceAmount: 1);
        var priceItem = MakePlayerItem(qty: 10_000);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: 10_000));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // Act: 수량=100, MaxPerCall=null → 허용
        var result = await _sut.BuyAsync(1, 10, "req-unlim", 100);

        // Assert: MaxPerCallExceeded 없이 Success
        Assert.Equal(ShopPurchaseStatus.Success, result.Status);
    }

    // ── 19. 가격 × 수량 오버플로우 → NotEnoughCurrency 반환 ─────────────────

    [Fact]
    public async Task BuyAsync_PriceTimesQuantityOverflow_HandledSafely()
    {
        // Arrange: priceAmount=int.MaxValue, quantity=2 → long 오버플로우 → NotEnoughCurrency
        var product = MakeProduct(priceAmount: int.MaxValue);
        _shopRepo.GetByIdAsync(10).Returns(product);
        // 재화 보유량은 충분하지만 총 가격이 int.MaxValue를 넘음
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(MakePlayerItem(qty: int.MaxValue));

        // Act: quantity=2 → totalPrice = int.MaxValue * 2 > int.MaxValue → NotEnoughCurrency
        var result = await _sut.BuyAsync(1, 10, "req-overflow", 2);

        // Assert: 오버플로우 안전 처리 — NotEnoughCurrency 반환
        Assert.Equal(ShopPurchaseStatus.NotEnoughCurrency, result.Status);
        // 트랜잭션 미진입
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task<ShopPurchaseResult>>>());
    }

    // ── 21. 취소된 지급 이력은 일일 한도 집계에서 제외 ──────────────────────
    // IsCancelled=true 행은 SumQuantityAsync mock 반환값에 포함되지 않아야 함을 검증
    // → mock이 취소 제외 후 합계(3)를 반환 → 잔여=DailyLimit-3=7 → 요청수량=5 통과

    [Fact]
    public async Task BuyAsync_CancelledGrantsExcluded_FromDailyLimitCount()
    {
        // Arrange: DailyLimit=10, 취소 전 합산=5, 취소 2건 → 유효 합산=3, 잔여=7, 요청=5 → 통과
        const int dailyLimit        = 10;
        const int sumExcludingCancelled = 3; // IsCancelled=true 건 제외 후 유효 수량
        const int requestQty        = 5;

        var product     = MakeProduct(dailyLimit: dailyLimit, priceAmount: 100);
        var priceItem   = MakePlayerItem(qty: 1000);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: 1000));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.DirectSuccess());

        // SumQuantityAsync가 취소된 행을 제외한 합계(3)를 반환 — DB 쿼리 레벨 필터링을 가정
        var sourceKeyPrefix = $"shop:1:10:";
        _grantRepo.SumQuantityAsync(1, RewardSourceType.ShopPurchase, sourceKeyPrefix, Arg.Any<DateTime>())
                  .Returns(sumExcludingCancelled);

        // Act: 잔여=7, 요청=5 → 한도 내 → 성공이어야 함
        var result = await _sut.BuyAsync(1, 10, "req-cancelled-excluded", requestQty);

        // Assert: Success — 취소 건이 한도에서 복원되어 구매 가능
        Assert.Equal(ShopPurchaseStatus.Success, result.Status);
        // SumQuantityAsync가 정확히 1회 호출되었는지 확인 (한도 체크 경로 진입 검증)
        await _grantRepo.Received(1).SumQuantityAsync(
            1, RewardSourceType.ShopPurchase, sourceKeyPrefix, Arg.Any<DateTime>());
    }

    // ── 20. 중복 요청 수량 5개 → 재화 전체(500) 복원 확인 ───────────────────

    [Fact]
    public async Task BuyAsync_Duplicate_WithQuantity5_RestoresFullDeduction()
    {
        // Arrange: priceAmount=100, quantity=5, 총 차감 예정=500 → Duplicate 시 전액 복원
        const int quantity = 5;
        const int priceAmount = 100;
        const int initialQty = 1000;

        var product   = MakeProduct(priceAmount: priceAmount);
        var priceItem = MakePlayerItem(qty: initialQty);
        var rewardTable = MakeRewardTable();

        _shopRepo.GetByIdAsync(10).Returns(product);
        _itemRepo.GetByPlayerAndItemAsync(1, product.PriceItemId)
                 .Returns(priceItem, MakePlayerItem(qty: initialQty));
        _rewardTableRepo.GetByIdWithEntriesAsync(product.RewardTableId).Returns(rewardTable);
        // 중복 요청 시뮬레이션
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
                         .Returns(GrantRewardResult.Duplicate());

        // Act
        var result = await _sut.BuyAsync(1, 10, "req-dup-q5", quantity);

        // Assert: Duplicate 반환 + 재화 전체 복원 (500 차감 예정이었으므로 1000으로 복원)
        Assert.Equal(ShopPurchaseStatus.Duplicate, result.Status);
        Assert.Equal(initialQty, priceItem.Quantity);
        await _auditLogService.DidNotReceive().RecordAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
}
