using Framework.Application.Common;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.AdminMatch;

// Admin 게임 결과 이력 조회 서비스 구현체 (IGameMatchRepository → IGameResultRepository)
public class AdminMatchService : IAdminMatchService
{
    private readonly IGameResultRepository _matchRepo;

    public AdminMatchService(IGameResultRepository matchRepo)
    {
        _matchRepo = matchRepo;
    }

    // 매치 목록 조회 (필터 + 페이지네이션)
    public async Task<PagedResultDto<MatchSummaryDto>> SearchAsync(MatchFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var (items, total) = await _matchRepo.SearchAsync(
            filter.MatchId, filter.PlayerId, filter.Tier, filter.State,
            filter.From, filter.To, page, pageSize);

        // 튜플 구조 (Match, ParticipantCount) — ParticipantCount는 서브쿼리 COUNT 결과
        var dtos = items.Select(x => new MatchSummaryDto(
            x.Match.Id,
            x.Match.Tier,
            x.Match.State,
            x.Match.StartedAt,
            x.Match.EndedAt,
            x.ParticipantCount
        )).ToList();

        return new PagedResultDto<MatchSummaryDto>(dtos, total, page, pageSize);
    }

    // 매치 상세 조회 (참가자 + 닉네임 포함)
    public async Task<MatchDetailDto?> GetByIdAsync(Guid matchId)
    {
        // 참가자 + 플레이어 프로필 포함 조회
        var match = await _matchRepo.GetByIdWithParticipantsAsync(matchId);
        if (match is null) return null;

        var participants = match.Participants.Select(p => new MatchParticipantDto(
            p.Id,
            p.PlayerId,
            // 닉네임: Player.Nickname 사용 (PlayerProfile에는 Nickname 없음)
            p.Player?.Nickname ?? "(알 수 없음)",
            p.HumanType,
            p.Score,
            p.Result
        )).ToList();

        return new MatchDetailDto(
            match.Id,
            match.Tier,
            match.State,
            match.StartedAt,
            match.EndedAt,
            participants
        );
    }
}
