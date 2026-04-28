// JWT 토큰 생성 인터페이스 (Api 레이어 구현체가 주입됨)
public interface IJwtTokenProvider
{
    // AccessToken 생성 (단기) — 내부 Id는 서버 전용, publicId만 클레임에 포함
    string GenerateAccessToken(int playerId, Guid publicId);

    // RefreshToken 생성 (장기) - 토큰 문자열과 만료 시간 반환
    (string token, DateTime expiresAt) GenerateRefreshToken();
}
