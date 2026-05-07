using Framework.Application.Features.Exp;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.Unit.Exp;

// ExpService 단위 테스트 — 경험치 추가 및 레벨업 로직 검증
public class ExpServiceTests
{
    // 공통 대리자 — 각 테스트에서 필요에 따라 재설정
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly ILevelTableProvider _levelTable;
    private readonly IUnitOfWork _uow;
    private readonly ExpService _sut;

    public ExpServiceTests()
    {
        _profileRepo = Substitute.For<IPlayerProfileRepository>();
        _levelTable  = Substitute.For<ILevelTableProvider>();
        _uow         = UnitOfWorkSubstitute.CreatePassthrough();

        // 제네릭 오버로드(IReadOnlyList<int> 반환)도 패스스루 설정
        UnitOfWorkSubstitute.ConfigurePassthrough<IReadOnlyList<int>>(_uow);

        _sut = new ExpService(_profileRepo, _levelTable, _uow, NullLogger<ExpService>.Instance);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    // 기본 PlayerProfile 인스턴스 생성 — 테스트별 필드만 덮어씀
    private static PlayerProfile MakeProfile(int playerId = 1, int level = 1, int exp = 0) =>
        new() { Id = playerId, PlayerId = playerId, Level = level, Exp = exp };

    // ── 1. 경험치가 0 이하면 트랜잭션 진입 없이 빈 리스트 반환 ──────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task AddExpAsync_ZeroOrNegativeExp_ReturnsEmptyWithoutTransaction(int expAmount)
    {
        // Act
        var result = await _sut.AddExpAsync(1, expAmount, "test");

        // Assert: 빈 리스트 반환, 트랜잭션 및 레포지토리 미호출
        Assert.Empty(result);
        await _uow.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default(Func<Task<IReadOnlyList<int>>>)!);
        await _profileRepo.DidNotReceiveWithAnyArgs().GetByPlayerIdAsync(default!);
    }

    // ── 2. 프로필이 없으면 빈 리스트 반환, UpdateAsync 미호출 ──────────────

    [Fact]
    public async Task AddExpAsync_ProfileNotFound_ReturnsEmpty()
    {
        // Arrange: 프로필 조회 결과 null 반환
        _profileRepo.GetByPlayerIdAsync(1).Returns((PlayerProfile?)null);
        _levelTable.MaxLevel.Returns(10);

        // Act
        var result = await _sut.AddExpAsync(1, 100, "test");

        // Assert
        Assert.Empty(result);
        await _profileRepo.DidNotReceive().UpdateAsync(Arg.Any<PlayerProfile>());
    }

    // ── 3. 이미 최대 레벨이면 경험치 추가 없이 빈 리스트 반환 ──────────────

    [Fact]
    public async Task AddExpAsync_AlreadyAtMaxLevel_ReturnsEmpty()
    {
        // Arrange: 현재 레벨 == 최대 레벨(10)
        var profile = MakeProfile(level: 10);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);

        // Act
        var result = await _sut.AddExpAsync(1, 999, "test");

        // Assert: 최대 레벨이므로 경험치 추가 없이 빈 결과
        Assert.Empty(result);
        await _profileRepo.DidNotReceive().UpdateAsync(Arg.Any<PlayerProfile>());
    }

    // ── 4. 경험치가 쌓이지만 임계값 미달 — 빈 리스트, UpdateAsync 1회 ────

    [Fact]
    public async Task AddExpAsync_ExpGainedButNotEnoughToLevelUp_ReturnsEmpty()
    {
        // Arrange: 레벨 1, 다음 레벨(2) 임계값=100, 추가 경험치=50
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(100);

        // Act
        var result = await _sut.AddExpAsync(1, 50, "test");

        // Assert: 레벨업 없음, 그러나 Exp 갱신을 위해 UpdateAsync는 1회 호출됨
        Assert.Empty(result);
        await _profileRepo.Received(1).UpdateAsync(profile);
    }

    // ── 5. 경험치가 정확히 임계값에 도달하면 레벨업 발생 ─────────────────

    [Fact]
    public async Task AddExpAsync_ExactThresholdReached_LevelsUp()
    {
        // Arrange: 레벨 1, 임계값(2)=100, 추가 경험치=100 (정확히 충족)
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(100);
        _levelTable.GetThreshold(3).Returns(int.MaxValue); // 레벨 3 임계값은 사실상 무한

        // Act
        var result = await _sut.AddExpAsync(1, 100, "test");

        // Assert: 레벨 2에 도달
        Assert.Equal([2], result);
    }

    // ── 6. 단일 레벨업 — 반환 목록에 도달 레벨 포함, Exp 갱신 확인 ────────

    [Fact]
    public async Task AddExpAsync_SingleLevelUp_ReturnsLevelList()
    {
        // Arrange: 레벨 1 → 2 레벨업, 잉여 경험치 50 남음
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(100);
        _levelTable.GetThreshold(3).Returns(300); // 추가 150으로는 도달 불가

        // Act
        var result = await _sut.AddExpAsync(1, 150, "test");

        // Assert: 레벨 2만 반환, 경험치는 150으로 세팅됨
        Assert.Equal([2], result);
        Assert.Equal(150, profile.Exp);
    }

    // ── 7. 다중 레벨업 — 오름차순 목록 반환 ────────────────────────────────

    [Fact]
    public async Task AddExpAsync_MultiLevelUp_ReturnsAscendingLevels()
    {
        // Arrange: 레벨 1, 경험치 300으로 레벨 2·3 동시 달성
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(100);
        _levelTable.GetThreshold(3).Returns(200);
        _levelTable.GetThreshold(4).Returns(500); // 300으로는 도달 불가

        // Act
        var result = await _sut.AddExpAsync(1, 300, "test");

        // Assert: 2, 3 순서로 반환
        Assert.Equal([2, 3], result);
    }

    // ── 8. 최대 레벨에서 초과 경험치 처리 — 최대 레벨까지만 레벨업 ─────────

    [Fact]
    public async Task AddExpAsync_ExcessExpAtMaxLevel_StopsAtMaxLevel()
    {
        // Arrange: 레벨 9, 최대 레벨=10, 임계값(10)=100, 경험치 99999
        var profile = MakeProfile(level: 9, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(10).Returns(100);

        // Act
        var result = await _sut.AddExpAsync(1, 99999, "test");

        // Assert: 10레벨만 반환 (11 이상 없음)
        Assert.Equal([10], result);
        Assert.Equal(10, profile.Level);
    }

    // ── 9. UpdateAsync 호출 시 UpdatedAt이 호출 시점 전후 범위 내인지 확인 ──

    [Fact]
    public async Task AddExpAsync_UpdatedAtSetToUtcNow()
    {
        // Arrange
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(int.MaxValue); // 레벨업 없이 UpdatedAt만 확인

        // UpdateAsync 호출 시 실제 profile 객체를 캡처
        PlayerProfile? captured = null;
        await _profileRepo.UpdateAsync(Arg.Do<PlayerProfile>(p => captured = p));

        var before = DateTime.UtcNow;

        // Act
        await _sut.AddExpAsync(1, 50, "test");

        var after = DateTime.UtcNow;

        // Assert: UpdatedAt이 호출 전후 사이 값이어야 함
        Assert.NotNull(captured);
        Assert.InRange(captured!.UpdatedAt, before, after);
    }

    // ── 10. 트랜잭션 람다가 정확히 1회 호출됨을 검증 ────────────────────────

    [Fact]
    public async Task AddExpAsync_TransactionLambdaCalledExactlyOnce()
    {
        // Arrange: 경험치 추가가 실제로 진행되도록 최소 설정
        var profile = MakeProfile(level: 1, exp: 0);
        _profileRepo.GetByPlayerIdAsync(1).Returns(profile);
        _levelTable.MaxLevel.Returns(10);
        _levelTable.GetThreshold(2).Returns(int.MaxValue);

        // Act
        await _sut.AddExpAsync(1, 50, "test");

        // Assert: 제네릭 오버로드가 정확히 1회 호출됨
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task<IReadOnlyList<int>>>>());
    }
}
