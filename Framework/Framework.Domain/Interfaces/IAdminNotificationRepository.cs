using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// Admin 알림 저장소 인터페이스
public interface IAdminNotificationRepository
{
    // 알림 추가 (DedupKey 중복 시 무시)
    Task AddIfNotDuplicateAsync(AdminNotification notification);
    // 미확인 알림 수 조회 (헤더 폴링용)
    Task<int> GetUnreadCountAsync();
    // 알림 목록 조회 (페이지네이션 + 필터)
    Task<(List<AdminNotification> Items, int TotalCount)> SearchAsync(
        AdminNotificationCategory? category, bool? isRead, int page, int pageSize);
    // 단건 읽음 처리
    Task<bool> MarkReadAsync(long id);
    // 단건 안읽음으로 되돌리기 — 대상 없으면 false 반환
    Task<bool> MarkUnreadAsync(long id);
    // 전체 읽음 처리
    Task<int> MarkAllReadAsync(AdminNotificationCategory? category);
    Task SaveChangesAsync();
}
