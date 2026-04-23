namespace Framework.Domain.Constants;

// 시스템 설정 키 상수 (SystemConfig 테이블의 Key 컬럼값)
public static class SystemConfigKeys
{
    // 일일 로그인 보상 자동 발송 활성화 여부
    public const string DailyLoginRewardEnabled = "daily_login_reward_enabled";

    // 점검 모드 활성화 여부 (수동 강제 on/off)
    public const string MaintenanceMode = "maintenance_mode";

    // 점검 예약 시작 시각 (ISO 8601, UTC)
    public const string MaintenanceStartAt = "maintenance_start_at";

    // 점검 예약 종료 시각 (ISO 8601, UTC)
    public const string MaintenanceEndAt = "maintenance_end_at";
}
