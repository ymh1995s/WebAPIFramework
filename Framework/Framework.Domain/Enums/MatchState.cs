namespace Framework.Domain.Enums;

// 매칭/게임 진행 상태
public enum MatchState
{
    // 플레이어 모집 대기 중
    Waiting,

    // 매칭 완료 (플레이어 확정), 게임 진행 중
    InProgress,

    // 게임 정상 종료
    Finished,

    // 게임 비정상 종료 (서버 오류, 강제 종료 등)
    Aborted,
}
