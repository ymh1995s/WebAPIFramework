using Framework.Application.Common;

namespace Framework.Application.Features.RewardGrant;

// 보상 지급 이력 조회 서비스 인터페이스 (Admin 전용 읽기 서비스)
public interface IRewardGrantQueryService
{
    // 필터 조건으로 지급 이력 목록 조회 (페이지네이션)
    Task<PagedResultDto<RewardGrantDto>> SearchAsync(RewardGrantFilterDto filter);

    // ID로 단건 상세 조회 (BundleSnapshot 포함)
    Task<RewardGrantDetailDto?> GetByIdAsync(int id);
}
