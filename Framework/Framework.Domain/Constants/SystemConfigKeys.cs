namespace Framework.Domain.Constants;

// 시스템 설정 키 상수 (SystemConfig 테이블의 Key 컬럼값)
public static class SystemConfigKeys
{
    // 점검 모드 활성화 여부 (수동 강제 on/off)
    public const string MaintenanceMode = "maintenance_mode";

    // 점검 예약 시작 시각 (ISO 8601, UTC)
    public const string MaintenanceStartAt = "maintenance_start_at";

    // 점검 예약 종료 시각 (ISO 8601, UTC)
    public const string MaintenanceEndAt = "maintenance_end_at";

    // 클라이언트 앱(Unity 빌드)의 강제 업데이트 기준 최소 버전
    // 이 버전 미만의 클라이언트는 앱스토어 업데이트를 강제함
    // 서버 버전과 무관 — 앱스토어에 배포된 Unity 빌드 버전만 관리
    public const string ClientAppMinVersion = "client_app_min_version";

    // 앱스토어에 현재 배포된 클라이언트 앱 최신 버전
    // 강제는 아니지만 소프트 업데이트 안내에 사용
    public const string ClientAppLatestVersion = "client_app_latest_version";

    // 현재 활성 연월 (YYYYMM 문자열, 예: "202604") — 월 전환 감지용
    public const string DailyRewardActiveMonth = "daily_reward_active_month";

    // 일일 로그인 보상 하루 기준 시각 — 시(KST), 기본값 0 (00:00 KST)
    public const string DailyRewardDayBoundaryHourKst = "daily_reward_day_boundary_hour_kst";

    // 일일 로그인 보상 하루 기준 시각 — 분(KST), 기본값 0 (00:00 KST)
    public const string DailyRewardDayBoundaryMinuteKst = "daily_reward_day_boundary_minute_kst";

    // 월 28회 초과 시 지급할 기본 보상 아이템 ID (빈 문자열 = 미설정)
    public const string DailyRewardDefaultItemId = "daily_reward_default_item_id";

    // 기본 보상 아이템 수량 (기본값 0 = 미발송)
    public const string DailyRewardDefaultItemCount = "daily_reward_default_item_count";
}
