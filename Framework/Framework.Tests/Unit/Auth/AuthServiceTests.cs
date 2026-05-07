using Framework.Application.Features.Auth;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Tests.Infrastructure;

namespace Framework.Tests.Unit.Auth;

// AuthService 단위 테스트 — GuestLoginAsync / RefreshAsync / LogoutAsync 검증
public class AuthServiceTests
{
    // ── 공통 대리자 ──────────────────────────────────────────────────────────

    private readonly IPlayerRepository _playerRepo;
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtTokenProvider _jwtProvider;
    private readonly IGoogleTokenVerifier _googleVerifier;
    private readonly IUnitOfWork _uow;
    private readonly IPlayerWithdrawalCleaner _withdrawalCleaner;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _playerRepo       = Substitute.For<IPlayerRepository>();
        _profileRepo      = Substitute.For<IPlayerProfileRepository>();
        _refreshTokenRepo = Substitute.For<IRefreshTokenRepository>();
        _jwtProvider      = Substitute.For<IJwtTokenProvider>();
        _googleVerifier   = Substitute.For<IGoogleTokenVerifier>();
        _withdrawalCleaner = Substitute.For<IPlayerWithdrawalCleaner>();

        // void 오버로드 패스스루 설정 (GuestLoginAsync의 신규 플레이어 생성 트랜잭션)
        _uow = UnitOfWorkSubstitute.CreatePassthrough();

        _sut = new AuthService(
            _playerRepo,
            _profileRepo,
            _refreshTokenRepo,
            _jwtProvider,
            _googleVerifier,
            _uow,
            _withdrawalCleaner);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    // 기본 Player 인스턴스 생성
    private static Player MakePlayer(
        int id = 1,
        string deviceId = "device-12345678",
        bool isBanned = false,
        DateTime? bannedUntil = null) =>
        new()
        {
            Id         = id,
            DeviceId   = deviceId,
            IsBanned   = isBanned,
            BannedUntil = bannedUntil
        };

    // 기본 RefreshToken 인스턴스 생성
    private static RefreshToken MakeRefreshToken(
        Player player,
        string tokenHash = "hash-abc",
        DateTime? expiresAt = null,
        DateTime? revokedAt = null) =>
        new()
        {
            PlayerId  = player.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            RevokedAt = revokedAt,
            Player    = player
        };

    // JWT 기본 고정값 설정 — AccessToken/RefreshToken 고정 문자열, 해시 매핑
    private void StubJwtDefaults()
    {
        _jwtProvider.GenerateAccessToken(Arg.Any<int>(), Arg.Any<Guid>())
                    .Returns("stub-access-token");
        _jwtProvider.GenerateRefreshToken()
                    .Returns(("stub-plain-token", DateTime.UtcNow.AddDays(30)));
        _jwtProvider.ComputeRefreshTokenHash(Arg.Any<string>())
                    .Returns(ci => $"hash-of-{ci.Arg<string>()}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // GuestLoginAsync 테스트 (7개)
    // ════════════════════════════════════════════════════════════════════════

    // ── 1. 신규 플레이어 → 트랜잭션 내 Player + Profile AddAsync 각 1회 ────

    [Fact]
    public async Task GuestLoginAsync_NewPlayer_CreatesPlayerAndProfileInTransaction()
    {
        // Arrange: DeviceId 미조회 → 신규 플레이어
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns((Player?)null);
        StubJwtDefaults();

        // Act
        var result = await _sut.GuestLoginAsync("device-12345678", null, null);

        // Assert: 트랜잭션 1회, Player·Profile AddAsync 각 1회, IsNewPlayer=true
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        await _playerRepo.Received(1).AddAsync(Arg.Any<Player>());
        await _profileRepo.Received(1).AddAsync(Arg.Any<PlayerProfile>());
        Assert.True(result.IsNewPlayer);
    }

    // ── 2. 신규 플레이어 닉네임 — DeviceId 앞 8자에 "Guest_" 접두어 ────────

    [Fact]
    public async Task GuestLoginAsync_NewPlayer_NicknameDerivedFromDeviceId()
    {
        // Arrange
        const string deviceId = "abcdef0123456789";
        _playerRepo.GetByDeviceIdAsync(deviceId).Returns((Player?)null);
        StubJwtDefaults();

        // AddAsync 호출 시 Player 캡처
        Player? captured = null;
        await _playerRepo.AddAsync(Arg.Do<Player>(p => captured = p));

        // Act
        await _sut.GuestLoginAsync(deviceId, null, null);

        // Assert: 닉네임은 "Guest_" + DeviceId 앞 8자
        Assert.NotNull(captured);
        Assert.Equal("Guest_abcdef01", captured!.Nickname);
    }

    // ── 3. DeviceId가 8자 미만이면 전체 길이를 닉네임에 사용 ─────────────

    [Fact]
    public async Task GuestLoginAsync_NewPlayer_ShortDeviceId_NicknameUsesFullDeviceId()
    {
        // Arrange
        const string deviceId = "abc";
        _playerRepo.GetByDeviceIdAsync(deviceId).Returns((Player?)null);
        StubJwtDefaults();

        Player? captured = null;
        await _playerRepo.AddAsync(Arg.Do<Player>(p => captured = p));

        // Act
        await _sut.GuestLoginAsync(deviceId, null, null);

        // Assert: 3자 DeviceId → "Guest_abc"
        Assert.NotNull(captured);
        Assert.Equal("Guest_abc", captured!.Nickname);
    }

    // ── 4. 기존 플레이어 → LastLoginAt 갱신, 트랜잭션 미호출, IsNewPlayer=false ─

    [Fact]
    public async Task GuestLoginAsync_ExistingPlayer_UpdatesLastLoginAt()
    {
        // Arrange: 기존 플레이어(밴 없음)
        var player = MakePlayer();
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns(player);
        StubJwtDefaults();

        var before = DateTime.UtcNow;

        // Act
        var result = await _sut.GuestLoginAsync("device-12345678", null, null);

        var after = DateTime.UtcNow;

        // Assert: UpdateAsync 1회, 트랜잭션 미호출, IsNewPlayer=false
        await _playerRepo.Received(1).UpdateAsync(player);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        Assert.False(result.IsNewPlayer);
        Assert.InRange(player.LastLoginAt, before, after);
    }

    // ── 5. 밴된 플레이어 → UnauthorizedAccessException, AddAsync 미호출 ────

    [Fact]
    public async Task GuestLoginAsync_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: IsBanned=true, BannedUntil=null → 영구밴
        var player = MakePlayer(isBanned: true, bannedUntil: null);
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns(player);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GuestLoginAsync("device-12345678", null, null));

        await _playerRepo.DidNotReceive().AddAsync(Arg.Any<Player>());
    }

    // ── 6. 신규 플레이어 RefreshToken에 IP·UserAgent 저장됨 ───────────────

    [Fact]
    public async Task GuestLoginAsync_NewPlayer_RefreshTokenStoresIpAndUserAgent()
    {
        // Arrange
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns((Player?)null);
        StubJwtDefaults();

        RefreshToken? storedToken = null;
        await _refreshTokenRepo.AddAsync(Arg.Do<RefreshToken>(t => storedToken = t));

        // Act
        await _sut.GuestLoginAsync("device-12345678", "1.2.3.4", "UnityClient/1.0");

        // Assert: 저장된 RefreshToken에 IP·UserAgent 반영
        Assert.NotNull(storedToken);
        Assert.Equal("1.2.3.4", storedToken!.IpAddress);
        Assert.Equal("UnityClient/1.0", storedToken.UserAgent);
    }

    // ── 7. 기존 플레이어 RefreshToken에도 IP·UserAgent 저장됨 ────────────

    [Fact]
    public async Task GuestLoginAsync_ExistingPlayer_RefreshTokenStoresIpAndUserAgent()
    {
        // Arrange
        var player = MakePlayer();
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns(player);
        StubJwtDefaults();

        RefreshToken? storedToken = null;
        await _refreshTokenRepo.AddAsync(Arg.Do<RefreshToken>(t => storedToken = t));

        // Act
        await _sut.GuestLoginAsync("device-12345678", "5.6.7.8", "UnityClient/2.0");

        // Assert
        Assert.NotNull(storedToken);
        Assert.Equal("5.6.7.8", storedToken!.IpAddress);
        Assert.Equal("UnityClient/2.0", storedToken.UserAgent);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RefreshAsync 테스트 (6개)
    // ════════════════════════════════════════════════════════════════════════

    // ── 8. 토큰 해시 미조회 → UnauthorizedAccessException("유효하지 않은") ──

    [Fact]
    public async Task RefreshAsync_TokenNotFound_ThrowsUnauthorized()
    {
        // Arrange: 해시 조회 결과 null
        _jwtProvider.ComputeRefreshTokenHash("bad-token").Returns("hash-of-bad-token");
        _refreshTokenRepo.GetByTokenHashAsync("hash-of-bad-token").Returns((RefreshToken?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshAsync("bad-token", null, null));

        Assert.Contains("유효하지 않은", ex.Message);
        await _refreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>());
    }

    // ── 9. 이미 폐기된 토큰 → UnauthorizedAccessException("폐기된") ─────────

    [Fact]
    public async Task RefreshAsync_RevokedToken_ThrowsUnauthorized()
    {
        // Arrange: RevokedAt이 과거 → 명시적 폐기
        var player = MakePlayer();
        var stored = MakeRefreshToken(player, revokedAt: DateTime.UtcNow.AddHours(-1));
        _jwtProvider.ComputeRefreshTokenHash("revoked-token").Returns("hash-revoked");
        _refreshTokenRepo.GetByTokenHashAsync("hash-revoked").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshAsync("revoked-token", null, null));

        Assert.Contains("폐기된", ex.Message);
        await _refreshTokenRepo.DidNotReceive().DeleteAsync(Arg.Any<RefreshToken>());
    }

    // ── 10. 만료된 토큰 → DeleteAsync + SaveChangesAsync 후 UnauthorizedAccessException ─

    [Fact]
    public async Task RefreshAsync_ExpiredToken_DeletesAndThrows()
    {
        // Arrange: ExpiresAt이 과거, RevokedAt=null
        var player = MakePlayer();
        var stored = MakeRefreshToken(player, expiresAt: DateTime.UtcNow.AddDays(-1));
        _jwtProvider.ComputeRefreshTokenHash("expired-token").Returns("hash-expired");
        _refreshTokenRepo.GetByTokenHashAsync("hash-expired").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshAsync("expired-token", null, null));

        Assert.Contains("만료", ex.Message);
        // 만료 토큰은 즉시 삭제 후 예외 발생
        await _refreshTokenRepo.Received(1).DeleteAsync(stored);
        await _refreshTokenRepo.Received(1).SaveChangesAsync();
    }

    // ── 11. 유효한 토큰이지만 플레이어가 밴됨 → UnauthorizedAccessException("정지된") ─

    [Fact]
    public async Task RefreshAsync_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: 영구밴 플레이어의 유효한 토큰
        var player = MakePlayer(isBanned: true, bannedUntil: null);
        var stored = MakeRefreshToken(player);
        _jwtProvider.ComputeRefreshTokenHash("valid-token").Returns("hash-valid");
        _refreshTokenRepo.GetByTokenHashAsync("hash-valid").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshAsync("valid-token", null, null));

        Assert.Contains("정지된", ex.Message);
    }

    // ── 12. 유효한 토큰 → 기존 삭제 + 새 토큰 AddAsync ──────────────────

    [Fact]
    public async Task RefreshAsync_ValidToken_DeletesOldAndIssuesNewToken()
    {
        // Arrange
        var player = MakePlayer();
        var stored = MakeRefreshToken(player, tokenHash: "hash-valid");

        // ComputeRefreshTokenHash를 람다로 설정 — 입력별 반환값을 딕셔너리처럼 처리
        // NSubstitute 특정 인자 Returns 중복 시 마지막 설정이 이전을 덮어쓸 수 있어 람다 방식 사용
        var hashMap = new Dictionary<string, string>
        {
            ["valid-token"]         = "hash-valid",
            ["stub-plain-new-token"] = "hash-new"
        };
        _jwtProvider.ComputeRefreshTokenHash(Arg.Any<string>())
                    .Returns(ci => hashMap.TryGetValue(ci.Arg<string>(), out var h) ? h : "hash-unknown");

        // DB 조회 — 해시 "hash-valid"에 대해서만 stored 반환
        _refreshTokenRepo.GetByTokenHashAsync("hash-valid").Returns(stored);

        // 새 토큰 발급용 고정값
        _jwtProvider.GenerateAccessToken(Arg.Any<int>(), Arg.Any<Guid>())
                    .Returns("stub-access-token");
        _jwtProvider.GenerateRefreshToken()
                    .Returns(("stub-plain-new-token", DateTime.UtcNow.AddDays(30)));

        // Act
        await _sut.RefreshAsync("valid-token", null, null);

        // Assert: 기존 토큰 삭제 1회, 새 토큰 추가 1회
        await _refreshTokenRepo.Received(1).DeleteAsync(stored);
        await _refreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>());
    }

    // ── 13. 평문 토큰을 해시로 변환하여 조회함을 검증 ─────────────────────

    [Fact]
    public async Task RefreshAsync_HashesIncomingPlainToken()
    {
        // Arrange
        const string plainToken = "plain-token-xyz";
        _jwtProvider.ComputeRefreshTokenHash(plainToken).Returns("hash-of-plain-token-xyz");
        _refreshTokenRepo.GetByTokenHashAsync("hash-of-plain-token-xyz").Returns((RefreshToken?)null);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshAsync(plainToken, null, null));

        // Assert: ComputeRefreshTokenHash가 정확히 1회 호출됨
        _jwtProvider.Received(1).ComputeRefreshTokenHash(plainToken);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LogoutAsync 테스트 (3개)
    // ════════════════════════════════════════════════════════════════════════

    // ── 14. 토큰이 존재하면 삭제 + SaveChangesAsync ────────────────────────

    [Fact]
    public async Task LogoutAsync_TokenExists_DeletesAndSaves()
    {
        // Arrange
        var player = MakePlayer();
        var stored = MakeRefreshToken(player);
        _jwtProvider.ComputeRefreshTokenHash("logout-token").Returns("hash-logout");
        _refreshTokenRepo.GetByTokenHashAsync("hash-logout").Returns(stored);

        // Act
        await _sut.LogoutAsync("logout-token");

        // Assert: 삭제 1회, SaveChanges 1회
        await _refreshTokenRepo.Received(1).DeleteAsync(stored);
        await _refreshTokenRepo.Received(1).SaveChangesAsync();
    }

    // ── 15. 토큰이 없으면 조용히 무시 (NoOp) ─────────────────────────────

    [Fact]
    public async Task LogoutAsync_TokenNotFound_SilentNoOp()
    {
        // Arrange: 해시 조회 결과 null
        _jwtProvider.ComputeRefreshTokenHash("nonexistent-token").Returns("hash-none");
        _refreshTokenRepo.GetByTokenHashAsync("hash-none").Returns((RefreshToken?)null);

        // Act: 예외 없이 정상 완료
        await _sut.LogoutAsync("nonexistent-token");

        // Assert: 삭제 및 SaveChanges 미호출
        await _refreshTokenRepo.DidNotReceive().DeleteAsync(Arg.Any<RefreshToken>());
        await _refreshTokenRepo.DidNotReceive().SaveChangesAsync();
    }

    // ── 16. 평문 토큰을 해시로 변환하여 조회함을 검증 ─────────────────────

    [Fact]
    public async Task LogoutAsync_HashesIncomingPlainToken()
    {
        // Arrange
        const string plainToken = "logout-plain-token";
        _jwtProvider.ComputeRefreshTokenHash(plainToken).Returns("hash-of-logout-plain-token");
        _refreshTokenRepo.GetByTokenHashAsync("hash-of-logout-plain-token").Returns((RefreshToken?)null);

        // Act
        await _sut.LogoutAsync(plainToken);

        // Assert: ComputeRefreshTokenHash가 정확히 1회 호출됨
        _jwtProvider.Received(1).ComputeRefreshTokenHash(plainToken);
    }
}
