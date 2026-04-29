using Framework.Domain.Enums;

namespace Framework.Application.Features.AdminMatch;

// 매치 참가자 DTO
public record MatchParticipantDto(
    int Id,
    int PlayerId,
    // 닉네임 (PlayerProfile에서 조인)
    string Nickname,
    HumanType HumanType,
    int? Score,
    MatchOutcome? Result
);

// 매치 목록용 요약 DTO
public record MatchSummaryDto(
    Guid Id,
    Tier Tier,
    MatchState State,
    DateTime StartedAt,
    DateTime? EndedAt,
    int ParticipantCount
);

// 매치 상세 DTO (참가자 포함)
public record MatchDetailDto(
    Guid Id,
    Tier Tier,
    MatchState State,
    DateTime StartedAt,
    DateTime? EndedAt,
    List<MatchParticipantDto> Participants
);

// 매치 검색 필터 DTO
public record MatchFilterDto(
    Guid? MatchId = null,
    int? PlayerId = null,
    Tier? Tier = null,
    MatchState? State = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20
);
