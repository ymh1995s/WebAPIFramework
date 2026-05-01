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

// Admin 알림 목록 응답 — 페이지네이션 포함
public record AdminNotificationListResponse(List<AdminNotificationDto> Items, int TotalCount);

// 전체 읽음 처리 결과 응답
public record MarkAllReadResponse(int UpdatedCount);
