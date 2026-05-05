namespace Framework.Domain.Constants;

// 시간 관련 공용 상수 — 게임 날짜 기반 로직에서 공통으로 사용
public static class TimeConstants
{
    // KST 오프셋 (UTC+9) — 일일 보상/PII 정리 등 게임 날짜 기반 로직 공용
    public static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);
}
