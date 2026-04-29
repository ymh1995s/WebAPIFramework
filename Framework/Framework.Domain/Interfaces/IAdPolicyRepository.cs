using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 광고 보상 정책 저장소 인터페이스
public interface IAdPolicyRepository
{
    // Network + PlacementId 조합으로 활성 정책 조회 (삭제된 것 제외)
    Task<AdPolicy?> FindAsync(AdNetworkType network, string placementId);

    // 전체 광고 정책 조회 (소프트 딜리트 제외)
    Task<List<AdPolicy>> GetAllAsync();

    // Admin 필터 검색 — network 필터 + 페이지네이션, 소프트 딜리트 제외
    Task<(List<AdPolicy> Items, int TotalCount)> SearchAsync(
        AdNetworkType? network, int page, int pageSize);

    // ID로 단건 조회 (소프트 딜리트 포함 — 관리 목적)
    Task<AdPolicy?> GetByIdAsync(int id);

    // 새 광고 정책 추가
    Task AddAsync(AdPolicy policy);

    // 변경사항 저장
    Task SaveChangesAsync();
}
