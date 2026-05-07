using Framework.Application.Features.Auth;
using Framework.Application.Features.Auth.Exceptions;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Tests.Infrastructure;

namespace Framework.Tests.Unit.Auth;

// AuthService 단위 테스트 — GuestLoginAsync / RefreshAsync / LogoutAsync / GoogleLoginAsync / WithdrawAsync / LinkGoogleAsync / ResolveGoogleConflictAsync 검증
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

    // 기본 Player 인스턴스 생성 — googleId를 지정하면 구글 연동 계정으로 생성
    private static Player MakePlayer(
        int id = 1,
        string deviceId = "device-12345678",
        bool isBanned = false,
        DateTime? bannedUntil = null,
        string? googleId = null) =>
        new()
        {
            Id          = id,
            DeviceId    = deviceId,
            IsBanned    = isBanned,
            BannedUntil = bannedUntil,
            GoogleId    = googleId
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

    // 구글 IdToken 검증 스텁 — 지정된 googleId를 반환하도록 설정
    private void StubGoogleVerifier(string googleId)
    {
        _googleVerifier.VerifyAsync(Arg.Any<string>()).Returns(googleId);
    }

    // PlayerProfile 인스턴스 생성 — Level 스텁용 헬퍼
    private static PlayerProfile MakeProfile(int playerId, int level = 1) =>
        new()
        {
            PlayerId = playerId,
            Level    = level
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

    // ── 5. 밴된 플레이어 → PlayerBannedException, AddAsync 미호출 ────

    [Fact]
    public async Task GuestLoginAsync_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: IsBanned=true, BannedUntil=null → 영구밴
        var player = MakePlayer(isBanned: true, bannedUntil: null);
        _playerRepo.GetByDeviceIdAsync("device-12345678").Returns(player);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PlayerBannedException>(
            () => _sut.GuestLoginAsync("device-12345678", null, null));

        Assert.Equal("AUTH_BANNED", ex.ErrorCode);
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

    // ── 8. 토큰 해시 미조회 → InvalidRefreshTokenException(AUTH_TOKEN_INVALID) ──

    [Fact]
    public async Task RefreshAsync_TokenNotFound_ThrowsUnauthorized()
    {
        // Arrange: 해시 조회 결과 null
        _jwtProvider.ComputeRefreshTokenHash("bad-token").Returns("hash-of-bad-token");
        _refreshTokenRepo.GetByTokenHashAsync("hash-of-bad-token").Returns((RefreshToken?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _sut.RefreshAsync("bad-token", null, null));

        Assert.Equal("AUTH_TOKEN_INVALID", ex.ErrorCode);
        await _refreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>());
    }

    // ── 9. 이미 폐기된 토큰 → RefreshTokenRevokedException(AUTH_TOKEN_REVOKED) ─────────

    [Fact]
    public async Task RefreshAsync_RevokedToken_ThrowsUnauthorized()
    {
        // Arrange: RevokedAt이 과거 → 명시적 폐기
        var player = MakePlayer();
        var stored = MakeRefreshToken(player, revokedAt: DateTime.UtcNow.AddHours(-1));
        _jwtProvider.ComputeRefreshTokenHash("revoked-token").Returns("hash-revoked");
        _refreshTokenRepo.GetByTokenHashAsync("hash-revoked").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<RefreshTokenRevokedException>(
            () => _sut.RefreshAsync("revoked-token", null, null));

        Assert.Equal("AUTH_TOKEN_REVOKED", ex.ErrorCode);
        await _refreshTokenRepo.DidNotReceive().DeleteAsync(Arg.Any<RefreshToken>());
    }

    // ── 10. 만료된 토큰 → DeleteAsync + SaveChangesAsync 후 RefreshTokenExpiredException ─

    [Fact]
    public async Task RefreshAsync_ExpiredToken_DeletesAndThrows()
    {
        // Arrange: ExpiresAt이 과거, RevokedAt=null
        var player = MakePlayer();
        var stored = MakeRefreshToken(player, expiresAt: DateTime.UtcNow.AddDays(-1));
        _jwtProvider.ComputeRefreshTokenHash("expired-token").Returns("hash-expired");
        _refreshTokenRepo.GetByTokenHashAsync("hash-expired").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<RefreshTokenExpiredException>(
            () => _sut.RefreshAsync("expired-token", null, null));

        Assert.Equal("AUTH_TOKEN_EXPIRED", ex.ErrorCode);
        // 만료 토큰은 즉시 삭제 후 예외 발생
        await _refreshTokenRepo.Received(1).DeleteAsync(stored);
        await _refreshTokenRepo.Received(1).SaveChangesAsync();
    }

    // ── 11. 유효한 토큰이지만 플레이어가 밴됨 → PlayerBannedException(AUTH_BANNED) ─

    [Fact]
    public async Task RefreshAsync_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: 영구밴 플레이어의 유효한 토큰
        var player = MakePlayer(isBanned: true, bannedUntil: null);
        var stored = MakeRefreshToken(player);
        _jwtProvider.ComputeRefreshTokenHash("valid-token").Returns("hash-valid");
        _refreshTokenRepo.GetByTokenHashAsync("hash-valid").Returns(stored);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PlayerBannedException>(
            () => _sut.RefreshAsync("valid-token", null, null));

        Assert.Equal("AUTH_BANNED", ex.ErrorCode);
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
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
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

    // ════════════════════════════════════════════════════════════════════════
    // GoogleLoginAsync 테스트 (6개, #17~#22)
    // ════════════════════════════════════════════════════════════════════════

    // ── 17. 신규 계정 → 트랜잭션 내 Player + Profile AddAsync 각 1회, IsNewPlayer=true ─

    [Fact]
    public async Task GoogleLoginAsync_NewAccount_CreatesPlayerAndProfileInTransaction()
    {
        // Arrange: GoogleId 미조회 → 신규 계정 생성 분기
        const string googleId = "google-uid-new";
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns((Player?)null);
        StubJwtDefaults();

        // AddAsync 호출 시 생성된 Player 캡처
        Player? captured = null;
        await _playerRepo.AddAsync(Arg.Do<Player>(p => captured = p));

        // Act
        var result = await _sut.GoogleLoginAsync("id-token", null, null, null);

        // Assert: 트랜잭션 1회, Player·Profile AddAsync 각 1회, IsNewPlayer=true
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        await _playerRepo.Received(1).AddAsync(Arg.Any<Player>());
        await _profileRepo.Received(1).AddAsync(Arg.Any<PlayerProfile>());
        Assert.True(result.IsNewPlayer);

        // 캡처된 Player의 GoogleId 및 닉네임 접두사 검증
        Assert.NotNull(captured);
        Assert.Equal(googleId, captured!.GoogleId);
        Assert.StartsWith("Player_", captured.Nickname);
    }

    // ── 18. 기존 계정 + 비인증 요청 → LastLoginAt 갱신, 트랜잭션 미호출, IsNewPlayer=false ─

    [Fact]
    public async Task GoogleLoginAsync_ExistingAccount_NoCurrentPlayer_UpdatesLastLoginAt()
    {
        // Arrange: GoogleId 조회 → 기존 플레이어(밴 없음), currentPlayerId = null
        const string googleId = "google-uid-existing";
        var existing = MakePlayer(id: 1, googleId: googleId);
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        StubJwtDefaults();

        // Act
        var result = await _sut.GoogleLoginAsync("id-token", null, null, null);

        // Assert: UpdateAsync 1회, 트랜잭션 미호출, IsNewPlayer=false
        await _playerRepo.Received(1).UpdateAsync(existing);
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        Assert.False(result.IsNewPlayer);
    }

    // ── 19. 기존 계정이 밴 상태 → PlayerBannedException(AUTH_BANNED), UpdateAsync 미호출 ─

    [Fact]
    public async Task GoogleLoginAsync_ExistingAccount_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: 영구밴 플레이어, currentPlayerId = null
        const string googleId = "google-uid-banned";
        var existing = MakePlayer(id: 1, googleId: googleId, isBanned: true, bannedUntil: null);
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PlayerBannedException>(
            () => _sut.GoogleLoginAsync("id-token", null, null, null));

        Assert.Equal("AUTH_BANNED", ex.ErrorCode);
        await _playerRepo.DidNotReceive().UpdateAsync(Arg.Any<Player>());
    }

    // ── 20. 동일 소유자 재인증 (정상 계정) → UpdateAsync 1회, 예외 없음, IsNewPlayer=false ─

    [Fact]
    public async Task GoogleLoginAsync_SameOwnerReAuth_NormalPlayer_UpdatesLastLogin()
    {
        // Arrange: existing.Id == currentPlayerId → 자기 재인증 분기
        const string googleId = "google-uid-self";
        var existing = MakePlayer(id: 10, googleId: googleId);
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        StubJwtDefaults();

        // Act: currentPlayerId = 10 (existing.Id와 동일)
        var result = await _sut.GoogleLoginAsync("id-token", 10, null, null);

        // Assert: UpdateAsync 1회, 예외 없음, IsNewPlayer=false
        await _playerRepo.Received(1).UpdateAsync(existing);
        Assert.False(result.IsNewPlayer);
    }

    // ── 20-1. 동일 소유자 재인증 (밴된 계정) → PlayerBannedException(AUTH_BANNED), UpdateAsync 미호출 ─

    [Fact]
    public async Task GoogleLoginAsync_SameOwnerReAuth_BannedPlayer_ThrowsUnauthorized()
    {
        // Arrange: existing.Id == currentPlayerId이지만 IsBanned=true → 밴 체크 후 예외 발생
        const string googleId = "google-uid-self-banned";
        var existing = MakePlayer(id: 10, googleId: googleId, isBanned: true);
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);

        // Act & Assert: 정지된 계정은 자기 재인증이라도 PlayerBannedException 발생
        var ex = await Assert.ThrowsAsync<PlayerBannedException>(
            () => _sut.GoogleLoginAsync("id-token", 10, null, null));

        Assert.Equal("AUTH_BANNED", ex.ErrorCode);
        // UpdateAsync 미호출 — 밴 체크에서 차단되어야 함
        await _playerRepo.DidNotReceive().UpdateAsync(Arg.Any<Player>());
    }

    // ── 21. 다른 계정 충돌 → GoogleAccountConflictException, PublicId·Level 검증 ─

    [Fact]
    public async Task GoogleLoginAsync_DifferentAccounts_ThrowsConflictWithSummaries()
    {
        // Arrange: existing(Id=10) vs currentPlayer(Id=20, GoogleId=null) — 계정 충돌
        const string googleId = "google-uid-conflict";
        var existing = MakePlayer(id: 10, googleId: googleId);
        var currentPlayer = MakePlayer(id: 20, googleId: null);
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        _playerRepo.GetByIdAsync(20).Returns(currentPlayer);

        // 레벨 정보: existing=5, currentPlayer=3
        _profileRepo.GetByPlayerIdAsync(10).Returns(MakeProfile(10, level: 5));
        _profileRepo.GetByPlayerIdAsync(20).Returns(MakeProfile(20, level: 3));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GoogleAccountConflictException>(
            () => _sut.GoogleLoginAsync("id-token", 20, null, null));

        // 기존 계정(existing) PublicId·Level 검증
        Assert.Equal(existing.PublicId, ex.ExistingPlayer.PlayerId);
        Assert.Equal(5, ex.ExistingPlayer.Level);

        // 현재 게스트 계정(currentPlayer) PublicId·Level 검증
        Assert.Equal(currentPlayer.PublicId, ex.CurrentGuestPlayer.PlayerId);
        Assert.Equal(3, ex.CurrentGuestPlayer.Level);
    }

    // ── 22. 현재 계정이 이미 다른 구글 계정 연동 → InvalidOperationException ─

    [Fact]
    public async Task GoogleLoginAsync_CurrentPlayerAlreadyLinked_ThrowsInvalidOperation()
    {
        // Arrange: existing(Id=10), currentPlayer(Id=20, GoogleId="other-google-id") — 병합 불가
        const string googleId = "google-uid-new-request";
        var existing = MakePlayer(id: 10, googleId: googleId);
        var currentPlayer = MakePlayer(id: 20, googleId: "other-google-id");
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        _playerRepo.GetByIdAsync(20).Returns(currentPlayer);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GoogleLoginAsync("id-token", 20, null, null));

        Assert.Contains("현재 계정은 이미 다른 구글 계정", ex.Message);
    }

    // ════════════════════════════════════════════════════════════════════════
    // WithdrawAsync 테스트 (3개, #23~#25)
    // ════════════════════════════════════════════════════════════════════════

    // ── 23. 정상 탈퇴 → 트랜잭션 내 익명화·토큰삭제·게임데이터 정리 각 1회 ─

    [Fact]
    public async Task WithdrawAsync_ActivePlayer_AnonymizesAndPurgesData()
    {
        // Arrange: 정상 계정(IsDeleted=false)
        var player = MakePlayer(id: 1);
        _playerRepo.GetByIdIncludingDeletedAsync(1).Returns(player);

        // Act
        await _sut.WithdrawAsync(1);

        // Assert: 트랜잭션 1회, 익명화·토큰삭제·게임데이터정리 각 1회
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        await _playerRepo.Received(1).WithdrawAnonymizeAsync(player);
        await _refreshTokenRepo.Received(1).DeleteAllByPlayerIdAsync(1);
        await _withdrawalCleaner.Received(1).PurgeGameDataAsync(1);
    }

    // ── 24. 이미 탈퇴된 계정 → 멱등 처리, 익명화·삭제 미호출 ──────────────

    [Fact]
    public async Task WithdrawAsync_AlreadyDeletedPlayer_IdempotentNoOp()
    {
        // Arrange: IsDeleted=true — 이미 탈퇴 처리된 계정
        var player = MakePlayer(id: 1);
        player.IsDeleted = true;
        _playerRepo.GetByIdIncludingDeletedAsync(1).Returns(player);

        // Act: 예외 없이 정상 종료
        await _sut.WithdrawAsync(1);

        // Assert: 익명화·토큰삭제·게임데이터정리 모두 미호출 (멱등 종료)
        await _playerRepo.DidNotReceive().WithdrawAnonymizeAsync(Arg.Any<Player>());
        await _refreshTokenRepo.DidNotReceive().DeleteAllByPlayerIdAsync(Arg.Any<int>());
        await _withdrawalCleaner.DidNotReceive().PurgeGameDataAsync(Arg.Any<int>());
    }

    // ── 25. 존재하지 않는 플레이어 → InvalidOperationException("플레이어를 찾을 수 없습니다") ─

    [Fact]
    public async Task WithdrawAsync_PlayerNotFound_ThrowsInvalidOperation()
    {
        // Arrange: 조회 결과 null
        _playerRepo.GetByIdIncludingDeletedAsync(999).Returns((Player?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.WithdrawAsync(999));

        Assert.Contains("플레이어를 찾을 수 없습니다", ex.Message);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LinkGoogleAsync 테스트 (3개, #26~#28)
    // ════════════════════════════════════════════════════════════════════════

    // ── 26. 정상 연동 → GoogleId 설정 + UpdateAsync·SaveChangesAsync 각 1회 ─

    [Fact]
    public async Task LinkGoogleAsync_Normal_LinksGoogleIdAndSaves()
    {
        // Arrange: GoogleId 미조회(미연동), player(GoogleId=null)
        const string googleId = "google-uid-link";
        StubGoogleVerifier(googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns((Player?)null);
        var player = MakePlayer(id: 1, googleId: null);
        _playerRepo.GetByIdAsync(1).Returns(player);

        // Act
        await _sut.LinkGoogleAsync(1, "id-token");

        // Assert: player.GoogleId 연결됨, UpdateAsync·SaveChangesAsync 각 1회
        Assert.Equal(googleId, player.GoogleId);
        await _playerRepo.Received(1).UpdateAsync(player);
        await _playerRepo.Received(1).SaveChangesAsync();
    }

    // ── 27. 동일 플레이어 이미 연동 → 멱등 처리, UpdateAsync 미호출 ─────────

    [Fact]
    public async Task LinkGoogleAsync_SamePlayerAlreadyLinked_IdempotentNoOp()
    {
        // Arrange: existing.Id == playerId → 자기 자신에게 이미 연동된 경우
        const string googleId = "google-uid-idempotent";
        StubGoogleVerifier(googleId);
        var existing = MakePlayer(id: 1, googleId: googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        _playerRepo.GetByIdAsync(1).Returns(existing);

        // Act: 예외 없이 정상 종료
        await _sut.LinkGoogleAsync(1, "id-token");

        // Assert: UpdateAsync 미호출 (멱등 종료)
        await _playerRepo.DidNotReceive().UpdateAsync(Arg.Any<Player>());
    }

    // ── 28. 다른 플레이어에 이미 연동 → GoogleAccountConflictException, PublicId·Level 검증 ─

    [Fact]
    public async Task LinkGoogleAsync_OtherPlayerHasGoogleId_ThrowsConflictWithSummaries()
    {
        // Arrange: existing(Id=10), player(Id=20) — 구글 계정 충돌
        const string googleId = "google-uid-conflict-link";
        StubGoogleVerifier(googleId);
        var existing = MakePlayer(id: 10, googleId: googleId);
        var player = MakePlayer(id: 20, googleId: null);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(existing);
        _playerRepo.GetByIdAsync(20).Returns(player);

        // 레벨 정보: existing=7, player=2
        _profileRepo.GetByPlayerIdAsync(10).Returns(MakeProfile(10, level: 7));
        _profileRepo.GetByPlayerIdAsync(20).Returns(MakeProfile(20, level: 2));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GoogleAccountConflictException>(
            () => _sut.LinkGoogleAsync(20, "id-token"));

        // PublicId·Level 검증
        Assert.Equal(existing.PublicId, ex.ExistingPlayer.PlayerId);
        Assert.Equal(7, ex.ExistingPlayer.Level);
        Assert.Equal(player.PublicId, ex.CurrentGuestPlayer.PlayerId);
        Assert.Equal(2, ex.CurrentGuestPlayer.Level);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ResolveGoogleConflictAsync 테스트 (3개, #29~#31)
    // ════════════════════════════════════════════════════════════════════════

    // ── 29. 정상 충돌 해소 → 트랜잭션 내 SoftDelete·토큰삭제·UpdateAsync, 결과 PlayerId 검증 ─

    [Fact]
    public async Task ResolveGoogleConflictAsync_Normal_SoftDeletesGuestAndIssuesToken()
    {
        // Arrange: playerA(구글 연동, Id=10), playerB(게스트, Id=20, GoogleId=null)
        const string googleId = "google-uid-resolve";
        StubGoogleVerifier(googleId);
        var playerA = MakePlayer(id: 10, googleId: googleId);
        var playerB = MakePlayer(id: 20, googleId: null);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(playerA);
        _playerRepo.GetByIdAsync(20).Returns(playerB);
        StubJwtDefaults();

        // Act: guestPlayerId=20 → playerB를 소프트딜리트하고 playerA로 토큰 발급
        var result = await _sut.ResolveGoogleConflictAsync(20, "id-token", null, null);

        // Assert: 트랜잭션 1회, SoftDelete·토큰삭제·UpdateAsync 각 1회
        await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        await _playerRepo.Received(1).SoftDeleteAsync(playerB, 10);
        await _refreshTokenRepo.Received(1).DeleteAllByPlayerIdAsync(20);
        await _playerRepo.Received(1).UpdateAsync(playerA);

        // 결과 PlayerId는 playerA의 PublicId
        Assert.Equal(playerA.PublicId, result.PlayerId);
    }

    // ── 30. 요청자가 이미 구글 계정 → 멱등 처리, 트랜잭션·SoftDelete 미호출, 토큰 정상 발급 ─

    [Fact]
    public async Task ResolveGoogleConflictAsync_RequesterIsAlreadyGoogleAccount_IdempotentTokenIssue()
    {
        // Arrange: playerA.Id == guestPlayerId → 이미 구글 연동 계정으로 해소 완료된 상태
        const string googleId = "google-uid-idempotent-resolve";
        StubGoogleVerifier(googleId);
        var playerA = MakePlayer(id: 10, googleId: googleId);
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(playerA);
        _playerRepo.GetByIdAsync(10).Returns(playerA);
        StubJwtDefaults();

        // Act: guestPlayerId=10 (playerA.Id와 동일) → 멱등 분기
        var result = await _sut.ResolveGoogleConflictAsync(10, "id-token", null, null);

        // Assert: 트랜잭션·SoftDelete 미호출, 토큰 정상 발급
        await _uow.DidNotReceive().ExecuteInTransactionAsync(Arg.Any<Func<Task>>());
        await _playerRepo.DidNotReceive().SoftDeleteAsync(Arg.Any<Player>(), Arg.Any<int>());
        Assert.Equal(playerA.PublicId, result.PlayerId);
    }

    // ── 31. 게스트 계정이 이미 다른 구글 계정 연동 → InvalidOperationException("게스트 계정이 이미 다른 구글 계정"), SoftDelete 미호출 ─

    [Fact]
    public async Task ResolveGoogleConflictAsync_GuestAlreadyLinked_ThrowsInvalidOperation()
    {
        // Arrange: playerB.GoogleId="other-google-id" → 게스트 계정이 이미 다른 구글 계정에 연동
        const string googleId = "google-uid-resolve-conflict";
        StubGoogleVerifier(googleId);
        var playerA = MakePlayer(id: 10, googleId: googleId);
        var playerB = MakePlayer(id: 20, googleId: "other-google-id");
        _playerRepo.GetByGoogleIdAsync(googleId).Returns(playerA);
        _playerRepo.GetByIdAsync(20).Returns(playerB);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ResolveGoogleConflictAsync(20, "id-token", null, null));

        Assert.Contains("게스트 계정이 이미 다른 구글 계정", ex.Message);
        await _playerRepo.DidNotReceive().SoftDeleteAsync(Arg.Any<Player>(), Arg.Any<int>());
    }
}
