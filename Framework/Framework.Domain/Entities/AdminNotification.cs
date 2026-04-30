using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// Admin 운영 알림 엔티티 — 범용 알림 시스템 (IAP 환불, 향후 밴/어뷰징 등)
public class AdminNotification
{
    public long Id { get; set; }
    // 알림 분류 — 필터링 및 UI 그룹핑에 사용
    public AdminNotificationCategory Category { get; set; }
    // 심각도 — UI 색상 구분 (Info=파랑, Warning=노랑, Critical=빨강)
    public AdminNotificationSeverity Severity { get; set; }
    // 알림 제목 (한 줄 요약)
    public string Title { get; set; } = string.Empty;
    // 상세 메시지
    public string Message { get; set; } = string.Empty;
    // 연관 엔티티 타입 (예: "IapPurchase")
    public string? RelatedEntityType { get; set; }
    // 연관 엔티티 ID — 상세 링크용
    public long? RelatedEntityId { get; set; }
    // 추가 컨텍스트 JSON (PlayerId, ProductId, OrderId 등)
    public string? MetadataJson { get; set; }
    // 중복 방지 키 — UNIQUE WHERE NOT NULL
    public string? DedupKey { get; set; }
    // 읽음 여부
    public bool IsRead { get; set; }
    // 읽음 처리 시각
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
