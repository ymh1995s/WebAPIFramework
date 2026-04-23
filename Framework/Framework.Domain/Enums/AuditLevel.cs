namespace Framework.Domain.Enums;

// 감사 로그 기록 수준 — 아이템 중요도에 따라 로그 저장 범위를 차별화하기 위한 정책값
//
// [정책 원칙]
//   중요 재화(유료/레어)      → Full        : 매번 기록
//   덜 중요한 재화(골드/경험치) → AnomalyOnly : 이상 징후만 기록
//
// [설계 의도]
// 모든 변동을 기록하면 일반 재화는 로그 볼륨이 폭증하므로,
// 덜 중요한 재화는 임계값(AnomalyThreshold)을 초과하는 비정상 변동만 남겨서
// "어뷰징 탐지"에는 충분하되 DB 용량은 아끼는 구조.
//
// [기본값]
// enum 순서상 AnomalyOnly = 0이므로 DB 기본값도 AnomalyOnly가 된다.
// 즉 "기본은 로그 안 남김, Admin이 명시적으로 Full로 올려야 함"을 의미.
public enum AuditLevel
{
    // 이상치(AnomalyThreshold 초과)로 판단된 변동만 기록 — 골드·경험치 등 수량 많은 일반 재화
    AnomalyOnly,

    // 모든 변동을 기록 — 유료 재화·레어 아이템 등 중요도 높은 재화
    Full
}
