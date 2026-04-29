using Framework.Application.Common;

namespace Framework.Application.Features.AdPolicy;

// 광고 정책 Admin 관리 서비스 인터페이스
public interface IAdPolicyService
{
    // 광고 정책 목록 조회 (필터 + 페이지네이션)
    Task<PagedResultDto<AdPolicyDto>> SearchAsync(AdPolicyFilterDto filter);

    // ID로 광고 정책 단건 조회
    Task<AdPolicyDto?> GetByIdAsync(int id);

    // 광고 정책 생성 — UNIQUE(Network, PlacementId) 위반 시 null 반환
    Task<AdPolicyDto?> CreateAsync(CreateAdPolicyDto dto);

    // 광고 정책 수정 (RewardTableId, DailyLimit, IsEnabled, Description 변경 가능)
    Task<bool> UpdateAsync(int id, UpdateAdPolicyDto dto);

    // 광고 정책 소프트 삭제 (IsDeleted = true)
    Task<bool> SoftDeleteAsync(int id);
}
