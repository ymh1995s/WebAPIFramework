using Framework.Domain.Enums;

namespace Framework.Application.DTOs;

// 매칭 참가 요청 DTO
public record JoinMatchRequestDto(string UserId, Tier Tier, HumanType HumanType = HumanType.Human);

// 대기열 내 유저 정보 (인-메모리 전용, DB 비저장)
public record MatchUserDto(string UserId, Tier Tier, HumanType HumanType, MatchState State = MatchState.Waiting);

// 매칭 결과 응답 DTO
public record MatchResultDto(
    bool IsMatched,           // 매칭 성사 여부
    bool IsDuplicate,         // 중복 참가 여부
    int WaitingCount,         // 현재 대기 인원
    List<MatchUserDto> Members,  // 매칭된 멤버 목록 (성사 시에만 유효)
    string Message,
    DateTime Timestamp
)
{
    // 대기 상태 응답 생성 헬퍼
    public static MatchResultDto Waiting(int waitingCount, string message) =>
        new(false, false, waitingCount, [], message, DateTime.UtcNow);

    // 매칭 성사 응답 생성 헬퍼
    public static MatchResultDto Matched(List<MatchUserDto> members, string message) =>
        new(true, false, members.Count, members, message, DateTime.UtcNow);

    // 중복 참가 응답 생성 헬퍼
    public static MatchResultDto Duplicate(string message) =>
        new(false, true, 0, [], message, DateTime.UtcNow);
}
