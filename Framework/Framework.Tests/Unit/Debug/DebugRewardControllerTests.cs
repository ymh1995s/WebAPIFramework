#if DEBUG

using System.Security.Claims;
using Framework.Api.Controllers.Player;
using Framework.Api.Requests;
using Framework.Application.Features.Reward;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.Unit.Debug;

// DebugRewardController 단위 테스트 — 디버그 보상 지급 엔드포인트 시나리오 검증
public class DebugRewardControllerTests
{
    // ── 공통 대리자 ──────────────────────────────────────────────────────────

    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IItemRepository _itemRepository;
    private readonly DebugRewardController _sut;

    // JWT 클레임에 주입할 기본 PlayerId
    private const int DefaultPlayerId = 42;

    public DebugRewardControllerTests()
    {
        _rewardDispatcher = Substitute.For<IRewardDispatcher>();
        _itemRepository = Substitute.For<IItemRepository>();

        _sut = new DebugRewardController(
            _rewardDispatcher,
            _itemRepository,
            NullLogger<DebugRewardController>.Instance);

        // JWT playerId 클레임 주입 — GetPlayerIdRequired() 확장이 사용하는 클레임
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = MakeUser(DefaultPlayerId)
            }
        };
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    // playerId 클레임을 포함하는 ClaimsPrincipal 생성
    private static ClaimsPrincipal MakeUser(int playerId) =>
        new(new ClaimsIdentity(new[] { new Claim("playerId", playerId.ToString()) }));

    // 아이템 존재성 검증 통과용 Item 목록 반환 설정
    private void SetupItemExists(params int[] itemIds)
    {
        var items = itemIds.Select(id => new Item { Id = id, Name = $"아이템{id}" }).ToList();
        _itemRepository.GetByIdsAsync(Arg.Any<List<int>>()).Returns(items);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 1: 아이템 + Exp 포함 → Dispatcher 호출, 200 반환
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_WithItemsAndExp_CallsDispatcher_ReturnsOk()
    {
        // Arrange
        SetupItemExists(10, 20);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
            .Returns(GrantRewardResult.DirectSuccess());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 500,
            Items: [new DebugGrantItem(10, 3), new DebugGrantItem(20, 1)]
        );

        // Act
        var result = await _sut.Grant(request);

        // Assert: 200 반환 + Dispatcher 정확히 1회 호출
        Assert.IsType<OkObjectResult>(result);
        await _rewardDispatcher.Received(1).GrantAsync(Arg.Any<GrantRewardRequest>());
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 2: Exp=0, Items 없음 → 400 반환 (지급할 내용 없음)
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_EmptyPayload_ReturnsBadRequest()
    {
        // Arrange: Exp=0, Items=null (빈 페이로드)
        var request = new DebugGrantRequest(PlayerId: null, Exp: 0, Items: null);

        // Act
        var result = await _sut.Grant(request);

        // Assert: 400 반환, Dispatcher 미호출
        Assert.IsType<BadRequestObjectResult>(result);
        await _rewardDispatcher.DidNotReceive().GrantAsync(Arg.Any<GrantRewardRequest>());
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 3: PlayerId 바디 오버라이드 → GrantRequest에 바디 PlayerId 사용
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_PlayerIdOverride_UsesPayloadPlayerId()
    {
        // Arrange: 바디에 PlayerId=99 지정 (JWT는 42)
        const int overridePlayerId = 99;
        SetupItemExists(10);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
            .Returns(GrantRewardResult.DirectSuccess());

        var request = new DebugGrantRequest(
            PlayerId: overridePlayerId,
            Exp: 100,
            Items: [new DebugGrantItem(10, 1)]
        );

        // Act
        await _sut.Grant(request);

        // Assert: GrantAsync에 바디 PlayerId(99) 전달 확인
        await _rewardDispatcher.Received(1).GrantAsync(
            Arg.Is<GrantRewardRequest>(r => r.PlayerId == overridePlayerId));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 4: PlayerId 미지정 → JWT 클레임 PlayerId 사용
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_NoPlayerIdInPayload_UsesJwtClaim()
    {
        // Arrange: PlayerId=null, JWT 클레임의 DefaultPlayerId(42) 사용
        SetupItemExists(10);
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
            .Returns(GrantRewardResult.DirectSuccess());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 50,
            Items: [new DebugGrantItem(10, 1)]
        );

        // Act
        await _sut.Grant(request);

        // Assert: GrantAsync에 JWT PlayerId(42) 전달 확인
        await _rewardDispatcher.Received(1).GrantAsync(
            Arg.Is<GrantRewardRequest>(r => r.PlayerId == DefaultPlayerId));
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 5: SourceKey=null → GrantRequest.SourceKey가 "debug:" 접두사로 시작하는 GUID 형식
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_SourceKeyNull_GeneratesDebugGuid()
    {
        // Arrange: SourceKey 미지정 → 자동 생성
        SetupItemExists(10);

        GrantRewardRequest? captured = null;
        _rewardDispatcher.GrantAsync(Arg.Do<GrantRewardRequest>(r => captured = r))
            .Returns(GrantRewardResult.DirectSuccess());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 100,
            Items: null,
            SourceKey: null
        );

        // Act
        await _sut.Grant(request);

        // Assert: SourceKey가 "debug:" 접두사로 시작하는지 확인
        Assert.NotNull(captured);
        Assert.StartsWith("debug:", captured!.SourceKey);
        // "debug:" 뒤에 내용이 있어야 함
        Assert.True(captured.SourceKey.Length > "debug:".Length);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 6: SourceKey 지정 → "debug:{SourceKey}" 형식 사용
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_SourceKeyProvided_UsesDebugPrefix()
    {
        // Arrange: SourceKey="test-key" 지정 → "debug:test-key"
        SetupItemExists(10);

        GrantRewardRequest? captured = null;
        _rewardDispatcher.GrantAsync(Arg.Do<GrantRewardRequest>(r => captured = r))
            .Returns(GrantRewardResult.DirectSuccess());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 100,
            Items: null,
            SourceKey: "test-key"
        );

        // Act
        await _sut.Grant(request);

        // Assert: SourceKey가 "debug:test-key" 형식인지 확인
        Assert.NotNull(captured);
        Assert.Equal("debug:test-key", captured!.SourceKey);
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 7: 미존재 ItemId 포함 → 400 반환
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_NonExistentItemId_ReturnsBadRequest()
    {
        // Arrange: ItemId=999 요청, DB에는 없음 (빈 목록 반환)
        _itemRepository.GetByIdsAsync(Arg.Any<List<int>>())
            .Returns(new List<Item>());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 0,
            Items: [new DebugGrantItem(999, 1)]
        );

        // Act
        var result = await _sut.Grant(request);

        // Assert: 400 반환, Dispatcher 미호출
        Assert.IsType<BadRequestObjectResult>(result);
        await _rewardDispatcher.DidNotReceive().GrantAsync(Arg.Any<GrantRewardRequest>());
    }

    // ════════════════════════════════════════════════════════════════════════
    // 시나리오 8: Dispatcher가 NotFound 반환 → 404 반환
    // ════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Grant_PlayerNotFound_Returns404()
    {
        // Arrange: Dispatcher가 플레이어 미존재 결과 반환
        _rewardDispatcher.GrantAsync(Arg.Any<GrantRewardRequest>())
            .Returns(GrantRewardResult.NotFound());

        var request = new DebugGrantRequest(
            PlayerId: null,
            Exp: 100,
            Items: null
        );

        // Act
        var result = await _sut.Grant(request);

        // Assert: 404 반환
        Assert.IsType<NotFoundObjectResult>(result);
    }
}

#endif
