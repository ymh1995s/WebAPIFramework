namespace Framework.Application.DTOs;

/// <summary>
/// 점검 예약 시각 설정 요청 DTO
/// </summary>
public record MaintenanceScheduleDto(DateTime? StartAt, DateTime? EndAt);
