using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 보상 지급 이력 저장소 구현체
public class RewardGrantRepository : IRewardGrantRepository
{
    private readonly AppDbContext _db;

    public RewardGrantRepository(AppDbContext db)
    {
        _db = db;
    }

    // PlayerId + SourceType + SourceKey 조합으로 기존 지급 이력 조회
    public async Task<RewardGrant?> FindAsync(int playerId, RewardSourceType sourceType, string sourceKey)
        => await _db.RewardGrants
            .FirstOrDefaultAsync(g =>
                g.PlayerId == playerId &&
                g.SourceType == sourceType &&
                g.SourceKey == sourceKey);

    // 새 지급 이력 추가 (UNIQUE 위반 시 DB에서 예외 발생)
    public async Task AddAsync(RewardGrant grant)
        => await _db.RewardGrants.AddAsync(grant);

    // 지급 이력 삭제 — 보상 지급 실패 시 선기록 롤백
    public Task DeleteAsync(RewardGrant grant)
    {
        _db.RewardGrants.Remove(grant);
        return Task.CompletedTask;
    }

    // Admin 필터 검색 — 페이지네이션, 기간, 플레이어 등 조건
    public async Task<(List<RewardGrant> Items, int TotalCount)> SearchAsync(
        int? playerId, RewardSourceType? sourceType, string? sourceKey,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = _db.RewardGrants.AsQueryable();

        // 플레이어 ID 필터
        if (playerId.HasValue)
            query = query.Where(g => g.PlayerId == playerId.Value);

        // SourceType 필터
        if (sourceType.HasValue)
            query = query.Where(g => g.SourceType == sourceType.Value);

        // SourceKey 부분 일치 필터
        if (!string.IsNullOrWhiteSpace(sourceKey))
            query = query.Where(g => g.SourceKey.Contains(sourceKey));

        // 기간 필터 (UTC 기준)
        if (from.HasValue)
            query = query.Where(g => g.GrantedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(g => g.GrantedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(g => g.GrantedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // ID로 단건 조회
    public async Task<RewardGrant?> GetByIdAsync(int id)
        => await _db.RewardGrants.FirstOrDefaultAsync(g => g.Id == id);

    // 오늘 광고 보상 지급 건수 조회 — 일일 한도 체크용
    // sourceKeyPrefix: "{network}:{placementId}:" 형태로 PlacementId별 카운트
    public async Task<int> CountTodayAsync(
        int playerId, RewardSourceType sourceType, string sourceKeyPrefix, DateTime utcDayStart)
        => await _db.RewardGrants
            .CountAsync(g =>
                g.PlayerId == playerId &&
                g.SourceType == sourceType &&
                g.SourceKey.StartsWith(sourceKeyPrefix) &&
                g.GrantedAt >= utcDayStart);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
