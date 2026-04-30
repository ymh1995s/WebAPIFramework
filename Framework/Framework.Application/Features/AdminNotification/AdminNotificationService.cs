using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;
// 엔티티 클래스와 현재 네임스페이스 충돌 방지를 위해 alias 사용
using AdminNotificationEntity = Framework.Domain.Entities.AdminNotification;

namespace Framework.Application.Features.AdminNotification;

// Admin 알림 서비스 구현체
public class AdminNotificationService : IAdminNotificationService
{
    private readonly IAdminNotificationRepository _repo;
    private readonly ILogger<AdminNotificationService> _logger;

    public AdminNotificationService(IAdminNotificationRepository repo, ILogger<AdminNotificationService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // 알림 생성 — DedupKey 중복 시 DB 레벨에서 무시
    public async Task CreateAsync(
        AdminNotificationCategory category, AdminNotificationSeverity severity,
        string title, string message,
        string? relatedEntityType = null, long? relatedEntityId = null,
        string? metadataJson = null, string? dedupKey = null)
    {
        var notification = new AdminNotificationEntity
        {
            Category = category, Severity = severity,
            Title = title, Message = message,
            RelatedEntityType = relatedEntityType, RelatedEntityId = relatedEntityId,
            MetadataJson = metadataJson, DedupKey = dedupKey,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.AddIfNotDuplicateAsync(notification);
        _logger.LogInformation("Admin 알림 생성 — Category: {Category}, Title: {Title}", category, title);
    }

    // 미확인 알림 수 조회 — 헤더 폴링용
    public async Task<int> GetUnreadCountAsync() => await _repo.GetUnreadCountAsync();

    // 알림 목록 조회 — DTO 변환 후 반환
    public async Task<(List<AdminNotificationDto> Items, int TotalCount)> SearchAsync(
        AdminNotificationCategory? category, bool? isRead, int page, int pageSize)
    {
        var (items, total) = await _repo.SearchAsync(category, isRead, page, pageSize);
        var dtos = items.Select(n => new AdminNotificationDto(
            n.Id, n.Category, n.Severity, n.Title, n.Message,
            n.RelatedEntityType, n.RelatedEntityId, n.MetadataJson,
            n.IsRead, n.ReadAt, n.CreatedAt)).ToList();
        return (dtos, total);
    }

    // 단건 읽음 처리
    public async Task<bool> MarkReadAsync(long id) => await _repo.MarkReadAsync(id);

    // 단건 안읽음으로 되돌리기 — repository 결과(존재 여부) 그대로 반환
    public async Task<bool> MarkUnreadAsync(long id) => await _repo.MarkUnreadAsync(id);

    // 전체 읽음 처리 — 카테고리 필터 선택적 적용
    public async Task<int> MarkAllReadAsync(AdminNotificationCategory? category) => await _repo.MarkAllReadAsync(category);
}
