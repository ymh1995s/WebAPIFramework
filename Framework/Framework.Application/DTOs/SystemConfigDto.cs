namespace Framework.Application.DTOs;

// 점검 예약 시각 설정 요청 DTO
public record MaintenanceScheduleDto(DateTime? StartAt, DateTime? EndAt);

// 버전 설정 요청 DTO
public record VersionConfigDto(string MinVersion, string LatestVersion);

// 일일 보상 하루 기준 시각 설정 요청 DTO (KST 기준 시/분)
public record DailyRewardDayBoundaryDto(int HourKst, int MinuteKst);

// 월 28회 초과 시 지급할 기본 보상 설정 DTO
public record DailyRewardDefaultDto(int? ItemId, int ItemCount);
