namespace Framework.Application.DTOs;

// 점검 예약 시각 설정 요청 DTO
public record MaintenanceScheduleDto(DateTime? StartAt, DateTime? EndAt);

// 버전 설정 요청 DTO
public record VersionConfigDto(string MinVersion, string LatestVersion);
