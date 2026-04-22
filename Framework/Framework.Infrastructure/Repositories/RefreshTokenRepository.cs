using Framework.Domain.Entities;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 리프래시 토큰 저장소 구현체
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    // 토큰 문자열로 조회
    public async Task<RefreshToken?> GetByTokenAsync(string token)
        => await _db.RefreshTokens.Include(r => r.Player).FirstOrDefaultAsync(r => r.Token == token);

    // 토큰 추가
    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _db.RefreshTokens.AddAsync(refreshToken);
        await _db.SaveChangesAsync();
    }

    // 단일 토큰 삭제 (로그아웃)
    public async Task DeleteAsync(RefreshToken refreshToken)
    {
        _db.RefreshTokens.Remove(refreshToken);
        await _db.SaveChangesAsync();
    }

    // 특정 플레이어의 모든 토큰 삭제 (강제 로그아웃)
    public async Task DeleteAllByPlayerIdAsync(int playerId)
    {
        var tokens = _db.RefreshTokens.Where(r => r.PlayerId == playerId);
        _db.RefreshTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync();
    }
}
