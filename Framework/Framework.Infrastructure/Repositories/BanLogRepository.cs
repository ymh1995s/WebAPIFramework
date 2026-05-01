using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 밴/밴해제 이력 저장소 구현체
// SearchAsync: Players 테이블과 LEFT JOIN하여 Nickname을 한 번에 조회 (N+1 쿼리 방지)
public class BanLogRepository : IBanLogRepository
{
    private readonly AppDbContext _context;

    public BanLogRepository(AppDbContext context)
    {
        _context = context;
    }

    // BanLog를 변경 추적에 등록 — SaveChanges는 호출자(AdminPlayerService) 책임
    public Task AddAsync(BanLog banLog)
    {
        _context.BanLogs.Add(banLog);
        return Task.CompletedTask;
    }

    // 필터 + 페이지네이션 기반 이력 검색
    // Players LEFT JOIN → 플레이어가 하드 삭제된 경우에도 BanLog 이력은 조회 가능
    // CreatedAt DESC 정렬 (최신순)
    public async Task<(List<(BanLog Log, string? Nickname)> Items, int TotalCount)> SearchAsync(
        int? playerId, BanAction? action, DateTime? from, DateTime? to, int page, int pageSize)
    {
        // BanLog + Player LEFT JOIN — IgnoreQueryFilters로 소프트 딜리트 플레이어도 닉네임 조회
        var query = from log in _context.BanLogs
                    join player in _context.Players.IgnoreQueryFilters()
                        on log.PlayerId equals player.Id into playerGroup
                    from p in playerGroup.DefaultIfEmpty()  // LEFT JOIN
                    select new { log, Nickname = (string?)p.Nickname };

        // 동적 필터 적용
        if (playerId.HasValue)
            query = query.Where(x => x.log.PlayerId == playerId.Value);

        if (action.HasValue)
            query = query.Where(x => x.log.Action == action.Value);

        if (from.HasValue)
            query = query.Where(x => x.log.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.log.CreatedAt <= to.Value);

        // 필터 적용 후 전체 건수 — DB 레벨 COUNT
        var totalCount = await query.CountAsync();

        // 최신순 정렬 + 페이지네이션 (Skip/Take DB 레벨 처리)
        var items = await query
            .OrderByDescending(x => x.log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new { x.log, x.Nickname })
            .ToListAsync();

        // 튜플 리스트로 변환하여 반환 — Nickname은 nullable (LEFT JOIN 결과)
        var result = items.Select(x => (x.log, (string?)x.Nickname)).ToList();
        return (result, totalCount);
    }
}
