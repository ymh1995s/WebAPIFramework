namespace Framework.Application.Interfaces;

// 시스템 설정 서비스 인터페이스
public interface ISystemConfigService
{
    // 점검 모드 활성화 여부 조회 (수동)
    Task<bool> GetMaintenanceModeAsync();
    // 점검 모드 활성화 여부 변경 (수동)
    Task SetMaintenanceModeAsync(bool enabled);

    // 점검 예약 시작 시각 조회
    Task<DateTime?> GetMaintenanceStartAtAsync();
    // 점검 예약 시작 시각 설정
    Task SetMaintenanceStartAtAsync(DateTime? dateTime);

    // 점검 예약 종료 시각 조회
    Task<DateTime?> GetMaintenanceEndAtAsync();
    // 점검 예약 종료 시각 설정
    Task SetMaintenanceEndAtAsync(DateTime? dateTime);

    // 현재 점검 중 여부 판단 (수동 ON 또는 예약 범위 내)
    Task<bool> IsUnderMaintenanceAsync();

    // 클라이언트 앱 강제 업데이트 기준 최소 버전 조회 (앱스토어 배포 버전 기준, 서버 버전 아님)
    Task<string> GetClientAppMinVersionAsync();
    // 클라이언트 앱 강제 업데이트 기준 최소 버전 설정
    Task SetClientAppMinVersionAsync(string version);

    // 앱스토어에 배포된 클라이언트 앱 최신 버전 조회
    Task<string> GetClientAppLatestVersionAsync();
    // 앱스토어에 배포된 클라이언트 앱 최신 버전 설정
    Task SetClientAppLatestVersionAsync(string version);

    // 일일 로그인 보상 하루 기준 시각 — 시(KST) 조회 (기본값 0)
    Task<int> GetDailyRewardDayBoundaryHourKstAsync();
    // 일일 로그인 보상 하루 기준 시각 — 분(KST) 조회 (기본값 0)
    Task<int> GetDailyRewardDayBoundaryMinuteKstAsync();
    // 일일 로그인 보상 하루 기준 시각 — 시(KST) 저장
    Task SetDailyRewardDayBoundaryHourKstAsync(int hour);
    // 일일 로그인 보상 하루 기준 시각 — 분(KST) 저장
    Task SetDailyRewardDayBoundaryMinuteKstAsync(int minute);

    // 월 28회 초과 시 지급할 기본 보상 아이템 ID 조회 (미설정 시 null)
    Task<int?> GetDailyRewardDefaultItemIdAsync();
    // 월 28회 초과 시 지급할 기본 보상 아이템 ID 저장 (null이면 빈 문자열로 저장)
    Task SetDailyRewardDefaultItemIdAsync(int? itemId);
    // 기본 보상 아이템 수량 조회 (미설정 시 0)
    Task<int> GetDailyRewardDefaultItemCountAsync();
    // 기본 보상 아이템 수량 저장
    Task SetDailyRewardDefaultItemCountAsync(int count);
}
