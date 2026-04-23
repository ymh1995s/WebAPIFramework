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

    // 클라이언트 앱(Unity 빌드)의 강제 업데이트 기준 최소 버전
    // 이 버전 미만의 클라이언트는 앱스토어 업데이트를 강제함
    // 서버 버전과 무관 — 앱스토어에 배포된 Unity 빌드 버전만 관리
    public const string ClientAppMinVersion = "client_app_min_version";

    // 앱스토어에 현재 배포된 클라이언트 앱 최신 버전
    // 강제는 아니지만 소프트 업데이트 안내에 사용
    public const string ClientAppLatestVersion = "client_app_latest_version";
}
