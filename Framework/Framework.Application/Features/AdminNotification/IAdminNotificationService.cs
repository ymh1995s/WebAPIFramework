using Framework.Domain.Enums;

namespace Framework.Application.Features.AdminNotification;

// Admin 알림 서비스 인터페이스
public interface IAdminNotificationService
{
    Task CreateAsync(
        AdminNotificationCategory category,
        AdminNotificationSeverity severity,
        string title,
        string message,
        string? relatedEntityType = null,
        long? relatedEntityId = null,
        string? metadataJson = null,
        string? dedupKey = null);
    Task<int> GetUnreadCountAsync();
    Task<(List<AdminNotificationDto> Items, int TotalCount)> SearchAsync(
        AdminNotificationCategory? category, bool? isRead, int page, int pageSize);
    Task<bool> MarkReadAsync(long id);
    // 단건 안읽음으로 되돌리기 — 대상 없으면 false 반환
    Task<bool> MarkUnreadAsync(long id);
    Task<int> MarkAllReadAsync(AdminNotificationCategory? category);
}
