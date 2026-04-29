using Framework.Application.Common;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.RewardGrant;

// 보상 지급 이력 조회 서비스 구현체 (Admin 전용 읽기)
public class RewardGrantQueryService : IRewardGrantQueryService
{
    private readonly IRewardGrantRepository _grantRepo;

    public RewardGrantQueryService(IRewardGrantRepository grantRepo)
    {
        _grantRepo = grantRepo;
    }

    // 필터 조건으로 지급 이력 목록 조회
    public async Task<PagedResultDto<RewardGrantDto>> SearchAsync(RewardGrantFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 50 : filter.PageSize;

        var (items, total) = await _grantRepo.SearchAsync(
            filter.PlayerId, filter.SourceType, filter.SourceKey,
            filter.From, filter.To, page, pageSize);

        var dtos = items.Select(g => new RewardGrantDto(
            g.Id,
            g.PlayerId,
            g.SourceType,
            g.SourceKey,
            g.GrantedAt,
            g.MailId.HasValue,
            g.MailId
        )).ToList();

        return new PagedResultDto<RewardGrantDto>(dtos, total, page, pageSize);
    }

    // ID로 단건 상세 조회 (BundleSnapshot 포함)
    public async Task<RewardGrantDetailDto?> GetByIdAsync(int id)
    {
        var grant = await _grantRepo.GetByIdAsync(id);
        if (grant is null) return null;

        return new RewardGrantDetailDto(
            grant.Id,
            grant.PlayerId,
            grant.SourceType,
            grant.SourceKey,
            grant.GrantedAt,
            grant.MailId.HasValue,
            grant.MailId,
            grant.BundleSnapshot
        );
    }
}
