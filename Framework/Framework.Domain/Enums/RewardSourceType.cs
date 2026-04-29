namespace Framework.Domain.Enums;

// 보상 발생 원천 타입 — 멱등성 키(SourceType + SourceKey) 구성에 사용
// 주의: enum 정수값을 DB에 저장하므로 기존 값은 절대 변경하지 말 것
public enum RewardSourceType
{
    // [사용 중단] 일일 로그인 보상 — RewardSlots에서 별도 관리되며 RewardTables를 참조하지 않음
    // RewardTables 생성 시 이 값은 사용하지 말 것 (DB 기존 데이터 호환을 위해 제거하지 않고 Obsolete 처리)
    [Obsolete("일일 로그인은 RewardSlots에서 관리 — RewardTables에서 사용 금지")]
    DailyLogin = 0,

    // 게임 매치 완료 보상
    MatchComplete = 1,

    // 퀘스트 완료 보상
    QuestComplete = 2,

    // 업적 달성 보상
    AchievementUnlock = 3,

    // 레벨업 보상
    LevelUp = 4,

    // 이벤트 보상
    EventReward = 5,

    // Admin 직접 지급 (운영툴에서 수동 지급)
    AdminGrant = 6,

    // 광고 시청 보상
    AdReward = 7,

    // 인앱 결제 보상
    Purchase = 8,

    // 스테이지 완료 보상
    StageComplete = 9,

    // 쿠폰/프로모 코드 보상
    CouponCode = 10,

    // 시즌 종료 랭킹 보상
    SeasonReward = 11,
}
