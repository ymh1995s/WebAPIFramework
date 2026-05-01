namespace Framework.Application.Features.SystemConfig;

// 점검 예약 시각 설정 요청 DTO
public record MaintenanceScheduleDto(DateTime? StartAt, DateTime? EndAt);

// 버전 설정 요청 DTO
public record VersionConfigDto(string MinVersion, string LatestVersion);

// 일일 보상 하루 기준 시각 설정 요청 DTO (KST 기준 시/분)
public record DailyRewardDayBoundaryDto(int HourKst, int MinuteKst);

// 월 28회 초과 시 지급할 기본 보상 설정 DTO
public record DailyRewardDefaultDto(int? ItemId, int ItemCount);

// 점검 모드 활성화 여부 응답 (요청 DTO와 별도)
public record MaintenanceModeResponse(bool Enabled);

// 점검 예약 일정 응답 (요청 DTO MaintenanceScheduleDto와 별도)
public record MaintenanceScheduleResponse(DateTime? StartAt, DateTime? EndAt);

// 점검 상태 조회 응답
public record MaintenanceStatusResponse(bool IsUnderMaintenance);

// 앱 버전 설정 응답 (요청 DTO VersionConfigDto와 별도)
public record VersionConfigResponse(string MinVersion, string LatestVersion);

// 일일 보상 기준 시각 응답 (요청 DTO DailyRewardDayBoundaryDto와 별도)
public record DailyRewardDayBoundaryResponse(int HourKst, int MinuteKst);
