namespace Framework.Domain.Enums;

// 광고 게재 위치 타입 — 리워드 비디오와 인터스티셜 구분
// 주의: DB에 정수값으로 저장되므로 기존 값은 절대 변경하지 말 것
public enum AdPlacementType
{
    // 리워드 비디오 광고 — 시청 완료 시 보상 지급
    RewardedVideo = 1,

    // 전면 광고 — 보상 없는 전면 노출 광고 (정책에 따라 보상 지급 가능)
    Interstitial = 2,
}
