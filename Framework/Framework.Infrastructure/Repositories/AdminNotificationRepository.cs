using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// Admin 알림 저장소 구현체
public class AdminNotificationRepository : IAdminNotificationRepository
{
    private readonly AppDbContext _db;

    public AdminNotificationRepository(AppDbContext db) { _db = db; }

    // 알림 추가 — DedupKey 있으면 사전 중복 확인 후 INSERT
    public async Task AddIfNotDuplicateAsync(AdminNotification notification)
    {
        if (notification.DedupKey is not null)
        {
            var exists = await _db.AdminNotifications.AnyAsync(n => n.DedupKey == notification.DedupKey);
            if (exists) return;
        }
        await _db.AdminNotifications.AddAsync(notification);
        await _db.SaveChangesAsync();
    }

    // 미확인 알림 수 조회
    public async Task<int> GetUnreadCountAsync()
        => await _db.AdminNotifications.CountAsync(n => !n.IsRead);

    // 알림 목록 조회 (최신순, 필터 + 페이지네이션)
    public async Task<(List<AdminNotification> Items, int TotalCount)> SearchAsync(
        AdminNotificationCategory? category, bool? isRead, int page, int pageSize)
    {
        var query = _db.AdminNotifications.AsQueryable();
        if (category.HasValue) query = query.Where(n => n.Category == category.Value);
        if (isRead.HasValue) query = query.Where(n => n.IsRead == isRead.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    // 단건 읽음 처리
    public async Task<bool> MarkReadAsync(long id)
    {
        var n = await _db.AdminNotifications.FindAsync(id);
        if (n is null) return false;
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // 단건 안읽음으로 되돌리기 — IsRead=false, ReadAt=null 초기화, 대상 없으면 false 반환
    public async Task<bool> MarkUnreadAsync(long id)
    {
        var n = await _db.AdminNotifications.FindAsync(id);
        if (n is null) return false;
        n.IsRead = false;
        n.ReadAt = null;
        await _db.SaveChangesAsync();
        return true;
    }

    // 전체 읽음 처리 — 미확인 항목만 대상으로 카테고리 선택 필터 적용
    public async Task<int> MarkAllReadAsync(AdminNotificationCategory? category)
    {
        var query = _db.AdminNotifications.Where(n => !n.IsRead);
        if (category.HasValue) query = query.Where(n => n.Category == category.Value);
        var items = await query.ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var item in items) { item.IsRead = true; item.ReadAt = now; }
        await _db.SaveChangesAsync();
        return items.Count;
    }

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
