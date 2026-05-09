using Framework.Application.Features.Tutorial;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.Unit.Tutorial;

// TutorialService 단위 테스트 — UpsertProgressAsync 검증
// [커버리지 범위] 신규 키 삽입 / 기존 키 갱신 / 한도 초과 / 길이 초과 / CountByPlayerIdAsync 미호출 보장
public class TutorialServiceTests
{
    // ── 공통 대리자 ──────────────────────────────────────────────────────────

    private readonly ITutorialProgressRepository _repo;
    private readonly TutorialService             _sut;

    public TutorialServiceTests()
    {
        _repo = Substitute.For<ITutorialProgressRepository>();
        _sut  = new TutorialService(_repo, NullLogger<TutorialService>.Instance);
    }

    // ── 1. 신규 키 + Count=200 → LimitExceeded, AddAsync/SaveChangesAsync 미호출 ──

    [Fact]
    public async Task UpsertProgressAsync_NewKey_At200Rows_ReturnsLimitExceeded()
    {
        // Arrange: 키 없음(신규), 현재 행 수 = 상한(200)
        _repo.GetByPlayerAndKeyAsync(1, "tutorial_step").Returns((TutorialProgress?)null);
        _repo.CountByPlayerIdAsync(1).Returns(200);

        // Act
        var result = await _sut.UpsertProgressAsync(1, "tutorial_step", "5");

        // Assert: LimitExceeded 반환, 저장 미호출
        Assert.Equal(TutorialUpsertStatus.LimitExceeded, result.Status);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
        await _repo.DidNotReceive().SaveChangesAsync();
    }

    // ── 2. 신규 키 + Count=199 → Success, AddAsync 1회 ───────────────────────

    [Fact]
    public async Task UpsertProgressAsync_NewKey_At199Rows_ReturnsSuccess()
    {
        // Arrange: 키 없음, 현재 행 수 = 상한-1(199) → 삽입 가능
        _repo.GetByPlayerAndKeyAsync(1, "new_key").Returns((TutorialProgress?)null);
        _repo.CountByPlayerIdAsync(1).Returns(199);

        // Act
        var result = await _sut.UpsertProgressAsync(1, "new_key", "value");

        // Assert: Success 반환, AddAsync 1회 호출
        Assert.Equal(TutorialUpsertStatus.Success, result.Status);
        await _repo.Received(1).AddAsync(Arg.Any<TutorialProgress>());
    }

    // ── 3. 기존 키 + Count=200 → Success, CountByPlayerIdAsync 미호출 ─────────
    // 기존 키 업데이트는 행 수 변화가 없으므로 한도 확인 절차 자체를 생략해야 함 (한도 우회 보장)

    [Fact]
    public async Task UpsertProgressAsync_ExistingKey_At200Rows_ReturnsSuccess()
    {
        // Arrange: 기존 키 존재, 행 수가 200이어도 업데이트는 허용
        var existing = new TutorialProgress
        {
            PlayerId  = 1,
            Key       = "existing_key",
            Value     = "old_value",
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };
        _repo.GetByPlayerAndKeyAsync(1, "existing_key").Returns(existing);

        // Act
        var result = await _sut.UpsertProgressAsync(1, "existing_key", "new_value");

        // Assert: Success 반환 + CountByPlayerIdAsync 미호출 (한도 체크 생략 확인)
        Assert.Equal(TutorialUpsertStatus.Success, result.Status);
        await _repo.DidNotReceive().CountByPlayerIdAsync(Arg.Any<int>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
    }

    // ── 4. 기존 키 업데이트 → Value 갱신 + UpdatedAt 현재 시각 범위 + AddAsync 미호출 ─

    [Fact]
    public async Task UpsertProgressAsync_ExistingKey_UpdatesValueAndUpdatedAt()
    {
        // Arrange: 기존 키 존재 — 이전 값/시각으로 초기화
        var existing = new TutorialProgress
        {
            PlayerId  = 1,
            Key       = "step_key",
            Value     = "old_value",
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };
        _repo.GetByPlayerAndKeyAsync(1, "step_key").Returns(existing);

        var before = DateTime.UtcNow;

        // Act
        var result = await _sut.UpsertProgressAsync(1, "step_key", "updated_value");

        var after = DateTime.UtcNow;

        // Assert: Value 갱신, UpdatedAt이 호출 전후 범위 이내, AddAsync 미호출
        Assert.Equal(TutorialUpsertStatus.Success, result.Status);
        Assert.Equal("updated_value", existing.Value);
        Assert.InRange(existing.UpdatedAt, before, after);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
    }

    // ── 5. 신규 키 + Count 50 → AddAsync 1회, 캡처된 entity 필드 검증 ────────

    [Fact]
    public async Task UpsertProgressAsync_NewKey_BelowLimit_AddsRow()
    {
        // Arrange: 신규 키, 여유 있는 행 수
        _repo.GetByPlayerAndKeyAsync(5, "my_step").Returns((TutorialProgress?)null);
        _repo.CountByPlayerIdAsync(5).Returns(50);

        // AddAsync 호출 시 entity 캡처
        TutorialProgress? captured = null;
        await _repo.AddAsync(Arg.Do<TutorialProgress>(p => captured = p));

        var before = DateTime.UtcNow;

        // Act
        var result = await _sut.UpsertProgressAsync(5, "my_step", "my_value");

        var after = DateTime.UtcNow;

        // Assert: 캡처된 entity 필드 검증
        Assert.Equal(TutorialUpsertStatus.Success, result.Status);
        Assert.NotNull(captured);
        Assert.Equal(5, captured!.PlayerId);
        Assert.Equal("my_step", captured.Key);
        Assert.Equal("my_value", captured.Value);
        // UpdatedAt이 호출 전후 범위 이내인지 검증
        Assert.InRange(captured.UpdatedAt, before, after);
    }

    // ── 6. Key 길이 초과 (129자) → KeyTooLong, repository 호출 일절 없음 ─────

    [Fact]
    public async Task UpsertProgressAsync_KeyTooLong_ReturnsKeyTooLong()
    {
        // Arrange: 128자 초과 (129자) 키
        var longKey = new string('a', 129);

        // Act
        var result = await _sut.UpsertProgressAsync(1, longKey, "value");

        // Assert: KeyTooLong 반환, repository 미호출
        Assert.Equal(TutorialUpsertStatus.KeyTooLong, result.Status);
        await _repo.DidNotReceive().GetByPlayerAndKeyAsync(Arg.Any<int>(), Arg.Any<string>());
        await _repo.DidNotReceive().CountByPlayerIdAsync(Arg.Any<int>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
        await _repo.DidNotReceive().SaveChangesAsync();
    }

    // ── 7. Value 길이 초과 (513자) → ValueTooLong, repository 호출 일절 없음 ──

    [Fact]
    public async Task UpsertProgressAsync_ValueTooLong_ReturnsValueTooLong()
    {
        // Arrange: 512자 초과 (513자) 값
        var longValue = new string('v', 513);

        // Act
        var result = await _sut.UpsertProgressAsync(1, "valid_key", longValue);

        // Assert: ValueTooLong 반환, repository 미호출
        Assert.Equal(TutorialUpsertStatus.ValueTooLong, result.Status);
        await _repo.DidNotReceive().GetByPlayerAndKeyAsync(Arg.Any<int>(), Arg.Any<string>());
        await _repo.DidNotReceive().CountByPlayerIdAsync(Arg.Any<int>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
        await _repo.DidNotReceive().SaveChangesAsync();
    }

    // ── 8. 신규 키 + Count=201 → LimitExceeded (비정상 상태 방어) ──────────────
    // 이미 200을 초과한 비정상 상태에서도 신규 삽입을 거부해야 함

    [Fact]
    public async Task UpsertProgressAsync_NewKey_At201Rows_ReturnsLimitExceeded()
    {
        // Arrange: 이미 상한을 넘어선 비정상 상태 — 방어적 처리 검증
        _repo.GetByPlayerAndKeyAsync(1, "new_key").Returns((TutorialProgress?)null);
        _repo.CountByPlayerIdAsync(1).Returns(201);

        // Act
        var result = await _sut.UpsertProgressAsync(1, "new_key", "value");

        // Assert: 201행이어도 LimitExceeded 반환
        Assert.Equal(TutorialUpsertStatus.LimitExceeded, result.Status);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TutorialProgress>());
    }
}
