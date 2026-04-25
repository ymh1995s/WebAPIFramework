using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;

// 인증 서비스 구현체
public class AuthService : IAuthService
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtTokenProvider _jwtProvider;
    private readonly IGoogleTokenVerifier _googleVerifier;

    public AuthService(
        IPlayerRepository playerRepo,
        IPlayerProfileRepository profileRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtTokenProvider jwtProvider,
        IGoogleTokenVerifier googleVerifier)
    {
        _playerRepo = playerRepo;
        _profileRepo = profileRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtProvider = jwtProvider;
        _googleVerifier = googleVerifier;
    }

    // 게스트 로그인 처리
    public async Task<TokenResponseDto> GuestLoginAsync(string deviceId)
    {
        // DeviceId로 기존 플레이어 조회
        var player = await _playerRepo.GetByDeviceIdAsync(deviceId);
        var isNew = player is null;

        if (isNew)
        {
            // 신규 플레이어 및 인게임 프로필 생성
            player = new Player
            {
                DeviceId = deviceId,
                Nickname = $"Guest_{deviceId[..Math.Min(8, deviceId.Length)]}"
            };
            await _playerRepo.AddAsync(player);

            // 인게임 프로필 초기화 (레벨 1, 재화 0)
            await _profileRepo.AddAsync(new PlayerProfile { PlayerId = player.Id });
        }
        else
        {
            // 마지막 로그인 시간 갱신
            player!.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(player);
        }

        return await IssueTokensAsync(player, isNew);
    }

    // 리프래시 토큰으로 AccessToken 재발급
    public async Task<TokenResponseDto> RefreshAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken)
            ?? throw new UnauthorizedAccessException("유효하지 않은 리프래시 토큰입니다.");

        if (stored.ExpiresAt < DateTime.UtcNow)
        {
            // 만료된 토큰 삭제 후 에러
            await _refreshTokenRepo.DeleteAsync(stored);
            throw new UnauthorizedAccessException("리프래시 토큰이 만료되었습니다.");
        }

        // 기존 토큰 삭제 후 새 토큰 발급 (토큰 교체 방식)
        await _refreshTokenRepo.DeleteAsync(stored);
        return await IssueTokensAsync(stored.Player, false);
    }

    // 로그아웃 - 리프래시 토큰 삭제
    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken);
        if (stored is not null)
            await _refreshTokenRepo.DeleteAsync(stored);
    }

    // 구글 로그인 - IdToken 검증 후 GoogleId로 플레이어 조회 또는 신규 생성
    public async Task<TokenResponseDto> GoogleLoginAsync(string idToken)
    {
        // 구글 서버에 IdToken 검증 요청 → GoogleId 획득
        var googleId = await _googleVerifier.VerifyAsync(idToken);

        var player = await _playerRepo.GetByGoogleIdAsync(googleId);
        var isNew = player is null;

        if (isNew)
        {
            // 구글 계정으로 신규 플레이어 생성
            player = new Player
            {
                DeviceId = Guid.NewGuid().ToString(),
                GoogleId = googleId,
                Nickname = $"Player_{googleId[..Math.Min(8, googleId.Length)]}"
            };
            await _playerRepo.AddAsync(player);

            // 인게임 프로필 초기화
            await _profileRepo.AddAsync(new PlayerProfile { PlayerId = player.Id });
        }
        else
        {
            // 마지막 로그인 시간 갱신
            player!.LastLoginAt = DateTime.UtcNow;
            await _playerRepo.UpdateAsync(player);
        }

        return await IssueTokensAsync(player, isNew);
    }

    // 계정 탈퇴 - 플레이어 삭제 시 CASCADE로 모든 연관 데이터 삭제됨
    public async Task WithdrawAsync(int playerId)
    {
        var player = await _playerRepo.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");

        await _playerRepo.DeleteAsync(player);
    }

    // 게스트 계정에 구글 연동 - 기존 데이터 유지하면서 GoogleId 추가
    public async Task LinkGoogleAsync(int playerId, string idToken)
    {
        var googleId = await _googleVerifier.VerifyAsync(idToken);

        // 이미 다른 계정에 연동된 구글 계정인지 확인
        var existing = await _playerRepo.GetByGoogleIdAsync(googleId);
        if (existing is not null)
            throw new InvalidOperationException("이미 다른 계정에 연동된 구글 계정입니다.");

        var player = await _playerRepo.GetByIdAsync(playerId)
            ?? throw new InvalidOperationException("플레이어를 찾을 수 없습니다.");

        // 기존 플레이어에 GoogleId 연결
        player.GoogleId = googleId;
        await _playerRepo.UpdateAsync(player);
    }

    // 토큰 생성 및 저장 공통 처리
    private async Task<TokenResponseDto> IssueTokensAsync(Player player, bool isNew)
    {
        var accessToken = _jwtProvider.GenerateAccessToken(player.Id);
        var (refreshTokenValue, expiresAt) = _jwtProvider.GenerateRefreshToken();

        // 리프래시 토큰 DB 저장
        await _refreshTokenRepo.AddAsync(new RefreshToken
        {
            PlayerId = player.Id,
            Token = refreshTokenValue,
            ExpiresAt = expiresAt
        });

        return new TokenResponseDto(accessToken, refreshTokenValue, player.Id, isNew);
    }
}
