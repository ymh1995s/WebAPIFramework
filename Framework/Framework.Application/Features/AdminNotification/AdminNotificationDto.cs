using Framework.Domain.Enums;

namespace Framework.Application.Features.AdminNotification;

// Admin 알림 목록 응답 DTO
public record AdminNotificationDto(
    long Id,
    AdminNotificationCategory Category,
    AdminNotificationSeverity Severity,
    string Title,
    string Message,
    string? RelatedEntityType,
    long? RelatedEntityId,
    string? MetadataJson,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);
