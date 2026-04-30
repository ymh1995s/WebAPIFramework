using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 1회 공지 저장소 EF Core 구현체
public class ShoutRepository : IShoutRepository
{
    private readonly AppDbContext _context;

    public ShoutRepository(AppDbContext context)
    {
        _context = context;
    }

    // 플레이어 접속 시 활성 1회 공지 조회
    // 조건: 활성(IsActive=true), 만료 전(ExpiresAt > 현재), 전체 대상(PlayerId=null) 또는 해당 플레이어만
    public async Task<List<Shout>> GetActiveForPlayerAsync(int playerId)
    {
        var now = DateTime.UtcNow;
        return await _context.Shouts
            .Where(s => s.IsActive && s.ExpiresAt > now
                        && (s.PlayerId == null || s.PlayerId == playerId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    // 1회 공지 추가 후 엔티티 반환
    public async Task<Shout> AddAsync(Shout shout)
    {
        await _context.Shouts.AddAsync(shout);
        return shout;
    }

    // Admin 이력 조회 — 필터 조합 및 페이지네이션
    public async Task<(List<Shout> items, int total)> SearchAsync(int? playerId, bool? activeOnly, int page, int pageSize)
    {
        // 기본 쿼리 — 최신순 정렬
        var query = _context.Shouts.AsQueryable();

        // PlayerId 필터 적용
        if (playerId.HasValue)
            query = query.Where(s => s.PlayerId == playerId.Value);

        // 활성만 보기 필터 적용
        if (activeOnly == true)
            query = query.Where(s => s.IsActive);

        // 전체 건수 집계 (페이지네이션 UI용)
        var total = await query.CountAsync();

        // 페이지네이션 적용 — 최신순 정렬 후 슬라이싱
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // ID로 단건 조회
    public async Task<Shout?> GetByIdAsync(int id)
        => await _context.Shouts.FindAsync(id);

    // 변경 사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
