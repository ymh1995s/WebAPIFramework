using Framework.Domain.Entities;

// 리프래시 토큰 저장소 인터페이스
public interface IRefreshTokenRepository
{
    // 토큰 문자열로 조회
    Task<RefreshToken?> GetByTokenAsync(string token);

    // 토큰 추가
    Task AddAsync(RefreshToken refreshToken);

    // 토큰 삭제 (로그아웃)
    Task DeleteAsync(RefreshToken refreshToken);

    // 특정 플레이어의 모든 토큰 삭제 (강제 로그아웃)
    Task DeleteAllByPlayerIdAsync(int playerId);

    // 변경사항을 DB에 반영 — 호출자(Service)가 명시적으로 호출
    Task SaveChangesAsync();
}
