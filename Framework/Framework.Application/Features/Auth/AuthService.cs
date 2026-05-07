using Framework.Application.Features.Auth.Exceptions;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Auth;

// 인증 서비스 구현체
public class AuthService : IAuthService
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtTokenProvider _jwtProvider;
    private readonly IGoogleTokenVerifier _googleVerifier;
    // 신규 계정 생성 시 Player + Profile을 단일 트랜잭션으로 묶기 위한 UoW
    private readonly IUnitOfWork _unitOfWork;
    // 탈퇴 시 게임 진행 데이터 정리 서비스 (H-12 SoftDelete + PII 익명화 정책)
    private readonly IPlayerWithdrawalCleaner _playerWithdrawalCleaner;

    public AuthService(
        IPlayerRepository playerRepo,
        IPlayerProfileRepository profileRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtTokenProvider jwtProvider,
        IGoogleTokenVerifier googleVerifier,
        IUnitOfWork unitOfWork,
        IPlayerWithdrawalCleaner playerWithdrawalCleaner)
    {
        _playerRepo = playerRepo;
        _profileRepo = profileRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtProvider = jwtProvider;
        _googleVerifier = googleVerifier;
        _unitOfWork = unitOfWork;
        _playerWithdrawalCleaner = playerWithdrawalCleaner;
    }

    // 게스트 로그인 처리
    public async Task<TokenResponseDto> GuestLoginAsync(string deviceId, string? ipAddress, string? userAgent)
    {
        // DeviceId로 기존 플레이어 조회
        var player = await _playerRepo.GetByDeviceIdAsync(deviceId);
        var isNew = player is null;

        // 기존 플레이어인 경우 실효 밴 여부 확인 — 신규 가입(isNew)은 밴 대상 없음
        if (player is not null && player.IsEffectivelyBanned)
            throw new PlayerBannedException();

        if (isNew)
        {
            // 신규 플레이어 + 프로필을 단일 트랜잭션으로 생성 — 중간 실패 시 Player만 남는 불완전 상태 방지
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                player = new Player
                {
                    DeviceId = deviceId,
                    Nickname = $"Guest_{deviceId[..Math.Min(8, deviceId.Length)]}"
                };
                // Player 저장 — 트랜잭션 내 SaveChanges로 Id 확정, 아직 미커밋 상태
                await _playerRepo.AddAsync(player);
                // 네비게이션 프로퍼티로 부모 관계 등록 — EF가 INSERT 순서 자동 결정 및 Id 전파
                await _profileRepo.AddAsync(new PlayerProfile { Player = player });
            });
        }
        else
        {
            // 마지막 로그인 시간 갱신
            player!.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(player);
            await _playerRepo.SaveChangesAsync();
        }

        // player는 신규 생성(isNew) 분기에서 트랜잭션 내 할당이 보장됨 — null! 억제
        return await IssueTokensAsync(player!, isNew, ipAddress, userAgent);
    }

    // 리프래시 토큰으로 AccessToken 재발급
    public async Task<TokenResponseDto> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent)
    {
        // 입력 평문 토큰을 해시로 변환하여 DB 조회 — 평문이 DB에 없으므로 반드시 해시 비교
        var tokenHash = _jwtProvider.ComputeRefreshTokenHash(refreshToken);
        var stored = await _refreshTokenRepo.GetByTokenHashAsync(tokenHash)
            ?? throw new InvalidRefreshTokenException();

        // 명시적 폐기 여부 확인 (강제 로그아웃 등)
        if (stored.RevokedAt is not null)
            throw new RefreshTokenRevokedException();

        if (stored.ExpiresAt < DateTime.UtcNow)
        {
            // 만료된 토큰 삭제 후 즉시 flush (예외 전 DB 정리)
            await _refreshTokenRepo.DeleteAsync(stored);
            await _refreshTokenRepo.SaveChangesAsync();
            throw new RefreshTokenExpiredException();
        }

        // 토큰 갱신 시점에도 실효 밴 여부 재확인 — 로그인 후 밴 처리된 계정의 추가 갱신 차단
        if (stored.Player.IsEffectivelyBanned)
            throw new PlayerBannedException();

        // 기존 토큰 삭제 후 새 토큰 발급 (토큰 교체 방식)
        // DeleteAsync는 ChangeTracker에만 등록 — IssueTokensAsync의 SaveChanges에서 Delete + Add 함께 flush
        await _refreshTokenRepo.DeleteAsync(stored);
        return await IssueTokensAsync(stored.Player, false, ipAddress, userAgent);
    }

    // 로그아웃 - 리프래시 토큰 삭제
    public async Task LogoutAsync(string refreshToken)
    {
        // 평문 토큰을 해시로 변환하여 DB 조회
        var tokenHash = _jwtProvider.ComputeRefreshTokenHash(refreshToken);
        var stored = await _refreshTokenRepo.GetByTokenHashAsync(tokenHash);
        if (stored is not null)
        {
            await _refreshTokenRepo.DeleteAsync(stored);
            await _refreshTokenRepo.SaveChangesAsync();
        }
    }

    // 구글 로그인 - IdToken 검증 후 GoogleId로 플레이어 조회 또는 신규 생성
    // currentPlayerId: 게스트 상태로 호출한 경우 JWT에서 추출한 플레이어 ID (없으면 null)
    public async Task<TokenResponseDto> GoogleLoginAsync(string idToken, int? currentPlayerId, string? ipAddress, string? userAgent)
    {
        // 구글 서버에 IdToken 검증 요청 → GoogleId 획득
        var googleId = await _googleVerifier.VerifyAsync(idToken);

        var existing = await _playerRepo.GetByGoogleIdAsync(googleId);

        // [분기 A] GoogleId를 가진 계정이 없음 → 신규 생성
        if (existing is null)
        {
            // 신규 플레이어 + 프로필을 단일 트랜잭션으로 생성 — GuestLoginAsync와 동일 원자성 보장
            Player newPlayer = null!;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                newPlayer = new Player
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    GoogleId = googleId,
                    Nickname = $"Player_{googleId[..Math.Min(8, googleId.Length)]}"
                };
                // Player 저장 — 트랜잭션 내 Id 확정
                await _playerRepo.AddAsync(newPlayer);
                // 네비게이션 프로퍼티로 부모 관계 등록 — EF가 INSERT 순서 자동 결정 및 Id 전파
                await _profileRepo.AddAsync(new PlayerProfile { Player = newPlayer });
            });
            return await IssueTokensAsync(newPlayer, true, ipAddress, userAgent);
        }

        // [분기 B] 비인증 요청(currentPlayerId == null) → 기존 계정으로 정상 재로그인
        if (currentPlayerId is null)
        {
            // 실효 밴 여부 확인 — 정지 기간이 남아있으면 로그인 거부
            if (existing.IsEffectivelyBanned)
                throw new PlayerBannedException();

            existing.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(existing);
            await _playerRepo.SaveChangesAsync();
            return await IssueTokensAsync(existing, false, ipAddress, userAgent);
        }

        // [분기 C] 요청자가 이미 이 구글 계정의 소유자 → 자기 재인증
        if (existing.Id == currentPlayerId.Value)
        {
            // 실효 밴 여부 확인 — 자기 재인증이라도 정지 계정은 토큰 발급 차단
            if (existing.IsEffectivelyBanned)
                throw new PlayerBannedException();

            existing.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(existing);
            await _playerRepo.SaveChangesAsync();
            return await IssueTokensAsync(existing, false, ipAddress, userAgent);
        }

        // [분기 D] 요청자(게스트)와 GoogleId 보유자가 다른 계정 → 충돌 처리
        var currentPlayer = await _playerRepo.GetByIdAsync(currentPlayerId.Value)
            ?? throw new InvalidOperationException("현재 플레이어를 찾을 수 없습니다.");

        // 요청자가 이미 다른 구글 계정에 연동된 경우 — 병합 불가
        if (currentPlayer.GoogleId is not null)
            throw new InvalidOperationException("현재 계정은 이미 다른 구글 계정에 연동되어 있습니다.");

        // 충돌 예외에 포함할 두 계정의 레벨 정보를 미리 조회 — 컨트롤러에서 별도 DB 접근 불필요
        var existingProfile = await _profileRepo.GetByPlayerIdAsync(existing.Id);
        var currentProfile  = await _profileRepo.GetByPlayerIdAsync(currentPlayer.Id);

        // PlayerSummaryDto로 조립하여 예외에 전달 — 내부 정수 Id 노출 없이 PublicId(Guid) 사용
        throw new GoogleAccountConflictException(
            new PlayerSummaryDto(existing.PublicId, existing.Nickname,
                existingProfile?.Level ?? 1, existing.CreatedAt, existing.LastLoginAt),
            new PlayerSummaryDto(currentPlayer.PublicId, currentPlayer.Nickname,
                currentProfile?.Level ?? 1, currentPlayer.CreatedAt, currentPlayer.LastLoginAt)
        );
    }

    // 계정 탈퇴 — SoftDelete + PII 익명화 방식 (H-12 IapPurchase Restrict FK 충돌 해소)
    // Player 행은 IapPurchase 결제 이력 보존을 위해 hard delete하지 않음
    public async Task WithdrawAsync(int playerId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 멱등성(M2): GlobalQueryFilter 우회 조회 → 이미 탈퇴된 계정은 즉시 정상 반환
            // 네트워크 재시도나 중복 호출에도 안전하게 204 응답
            var player = await _playerRepo.GetByIdIncludingDeletedAsync(playerId);
            if (player is null) throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");
            if (player.IsDeleted) return; // 이미 탈퇴 처리된 계정 — 멱등 처리

            // 1. PII 익명화 + IsDeleted=true
            //    (Player 행 자체는 보존 — IapPurchase FK 무결성 유지)
            await _playerRepo.WithdrawAnonymizeAsync(player);

            // 2. 모든 RefreshToken 즉시 무효화 — 탈퇴 후 세션 재사용 차단
            await _refreshTokenRepo.DeleteAllByPlayerIdAsync(playerId);

            // 3. 게임 진행 데이터 정리 — 재가입 시 데이터 복구 차단
            await _playerWithdrawalCleaner.PurgeGameDataAsync(playerId);
            // SaveChangesAsync + CommitAsync는 ExecuteInTransactionAsync 소유자가 자동 처리
        });
    }

    // 게스트 계정에 구글 연동 - 기존 데이터 유지하면서 GoogleId 추가
    // 충돌 시 GoogleAccountConflictException 발생
    public async Task LinkGoogleAsync(int playerId, string idToken)
    {
        var googleId = await _googleVerifier.VerifyAsync(idToken);

        // 이미 다른 계정에 연동된 구글 계정인지 확인
        var existing = await _playerRepo.GetByGoogleIdAsync(googleId);

        var player = await _playerRepo.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");

        if (existing is not null)
        {
            // 자기 자신에게 이미 연동된 경우는 멱등 처리 (에러 없음)
            if (existing.Id == playerId) return;

            // 충돌 예외에 포함할 두 계정의 레벨 정보를 미리 조회
            var existingProfile = await _profileRepo.GetByPlayerIdAsync(existing.Id);
            var currentProfile  = await _profileRepo.GetByPlayerIdAsync(player.Id);

            // PlayerSummaryDto로 조립하여 예외에 전달 — 컨트롤러에서 별도 DB 접근 불필요
            throw new GoogleAccountConflictException(
                new PlayerSummaryDto(existing.PublicId, existing.Nickname,
                    existingProfile?.Level ?? 1, existing.CreatedAt, existing.LastLoginAt),
                new PlayerSummaryDto(player.PublicId, player.Nickname,
                    currentProfile?.Level ?? 1, player.CreatedAt, player.LastLoginAt)
            );
        }

        // 기존 플레이어에 GoogleId 연결
        player.GoogleId = googleId;
        await _playerRepo.UpdateAsync(player);
        await _playerRepo.SaveChangesAsync();
    }

    // 구글 계정 충돌 해소 — 게스트 계정을 소프트 딜리트하고 구글 연동 계정으로 토큰 발급
    public async Task<TokenResponseDto> ResolveGoogleConflictAsync(int guestPlayerId, string idToken, string? ipAddress, string? userAgent)
    {
        // IdToken 재검증으로 충돌 해소 요청의 진위 확인
        var googleId = await _googleVerifier.VerifyAsync(idToken);

        var playerA = await _playerRepo.GetByGoogleIdAsync(googleId)
            ?? throw new InvalidOperationException("해당 구글 계정에 연동된 플레이어를 찾을 수 없습니다.");

        var playerB = await _playerRepo.GetByIdAsync(guestPlayerId)
            ?? throw new InvalidOperationException("현재 게스트 플레이어를 찾을 수 없습니다.");

        // 멱등 처리 — 요청자가 이미 구글 연동 계정인 경우 (중복 호출)
        if (playerB.Id == playerA.Id)
            return await IssueTokensAsync(playerA, false, ipAddress, userAgent);

        // 게스트 계정이 이미 다른 구글 계정에 연동된 경우 — 병합 불가
        if (playerB.GoogleId is not null)
            throw new InvalidOperationException("게스트 계정이 이미 다른 구글 계정에 연동되어 있어 병합할 수 없습니다.");

        // 소프트 딜리트 + 게스트 세션 토큰 삭제 + 기존 계정 LastLoginAt 갱신을 단일 트랜잭션으로 처리
        // 중간 실패 시 부분 적용 방지 (예: SoftDelete 후 토큰 삭제 실패로 게스트 토큰이 살아있는 상태)
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _playerRepo.SoftDeleteAsync(playerB, playerA.Id);
            await _refreshTokenRepo.DeleteAllByPlayerIdAsync(playerB.Id);

            playerA.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(playerA);
        });

        return await IssueTokensAsync(playerA, false, ipAddress, userAgent);
    }

    // 토큰 생성 및 저장 공통 처리 — publicId를 JWT 클레임 및 응답에 포함
    // ipAddress/userAgent: 발급 토큰에 기록할 보안 메타데이터 (포렌식·어뷰징 감지 용도)
    private async Task<TokenResponseDto> IssueTokensAsync(Player player, bool isNew, string? ipAddress, string? userAgent)
    {
        // 내부 Id와 공개 PublicId를 함께 전달하여 JWT 생성
        var accessToken = _jwtProvider.GenerateAccessToken(player.Id, player.PublicId);
        var (plainToken, expiresAt) = _jwtProvider.GenerateRefreshToken();

        // 평문 토큰을 SHA-256 해시로 변환 — DB에는 해시만 저장, 평문은 응답으로만 반환
        var tokenHash = _jwtProvider.ComputeRefreshTokenHash(plainToken);

        // 리프래시 토큰 DB 저장 후 flush — TokenHash + 보안 메타데이터 함께 기록
        await _refreshTokenRepo.AddAsync(new RefreshToken
        {
            PlayerId  = player.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await _refreshTokenRepo.SaveChangesAsync();

        // 응답에는 평문 토큰 반환 (클라이언트가 다음 Refresh 호출 시 사용)
        // 응답의 PlayerId는 외부 공개용 Guid (내부 정수 Id 미노출)
        return new TokenResponseDto(accessToken, plainToken, player.PublicId, isNew);
    }
}
