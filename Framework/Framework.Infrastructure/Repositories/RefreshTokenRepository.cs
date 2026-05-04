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

    // 토큰 해시(SHA-256 Base64)로 조회 — 평문 토큰 대신 해시로 비교
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        => await _db.RefreshTokens.Include(r => r.Player).FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

    // 토큰 추가 — SaveChanges는 호출자(Service)가 명시적으로 호출
    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _db.RefreshTokens.AddAsync(refreshToken);
    }

    // 단일 토큰 삭제 (로그아웃) — SaveChanges는 호출자(Service)가 명시적으로 호출
    public Task DeleteAsync(RefreshToken refreshToken)
    {
        _db.RefreshTokens.Remove(refreshToken);
        return Task.CompletedTask;
    }

    // 특정 플레이어의 모든 토큰 삭제 (강제 로그아웃) — SaveChanges는 호출자(Service)가 명시적으로 호출
    public Task DeleteAllByPlayerIdAsync(int playerId)
    {
        var tokens = _db.RefreshTokens.Where(r => r.PlayerId == playerId);
        _db.RefreshTokens.RemoveRange(tokens);
        return Task.CompletedTask;
    }

    // 변경사항을 DB에 반영 — 호출자(Service)가 명시적으로 호출
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
