using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 광고 보상 정책 저장소 구현체
public class AdPolicyRepository : IAdPolicyRepository
{
    private readonly AppDbContext _db;

    public AdPolicyRepository(AppDbContext db)
    {
        _db = db;
    }

    // Network + PlacementId 조합으로 활성 정책 조회 (삭제된 것 제외)
    public async Task<AdPolicy?> FindAsync(AdNetworkType network, string placementId)
        => await _db.AdPolicies
            .FirstOrDefaultAsync(p =>
                p.Network == network &&
                p.PlacementId == placementId &&
                !p.IsDeleted);

    // 전체 광고 정책 조회 (소프트 딜리트 제외)
    public async Task<List<AdPolicy>> GetAllAsync()
        => await _db.AdPolicies
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Network)
            .ThenBy(p => p.PlacementId)
            .ToListAsync();

    // Admin 필터 검색 — network 필터 + 페이지네이션
    public async Task<(List<AdPolicy> Items, int TotalCount)> SearchAsync(
        AdNetworkType? network, int page, int pageSize)
    {
        // 소프트 딜리트된 항목 제외
        var query = _db.AdPolicies
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // 네트워크 필터
        if (network.HasValue)
            query = query.Where(p => p.Network == network.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Network)
            .ThenBy(p => p.PlacementId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // ID로 단건 조회 (소프트 딜리트 포함 — 관리 목적)
    public async Task<AdPolicy?> GetByIdAsync(int id)
        => await _db.AdPolicies.FirstOrDefaultAsync(p => p.Id == id);

    // 새 광고 정책 추가
    public async Task AddAsync(AdPolicy policy)
        => await _db.AdPolicies.AddAsync(policy);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
