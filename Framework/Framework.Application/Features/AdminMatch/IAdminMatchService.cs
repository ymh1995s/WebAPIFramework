using Framework.Application.Common;

namespace Framework.Application.Features.AdminMatch;

// Admin 매치 이력 조회 서비스 인터페이스
public interface IAdminMatchService
{
    // 매치 목록 조회 (필터 + 페이지네이션)
    Task<PagedResultDto<MatchSummaryDto>> SearchAsync(MatchFilterDto filter);

    // 매치 상세 조회 (참가자 포함)
    Task<MatchDetailDto?> GetByIdAsync(Guid matchId);
}
