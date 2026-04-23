namespace Framework.Domain.Entities;

// 플레이어 재화/아이템 변동 감사 로그
// Item.AuditLevel 값에 따라 저장 여부가 결정됨 (Full = 전부, AnomalyOnly = 이상치만)
public class AuditLog
{
    public long Id { get; set; }

    // 변동 대상 플레이어
    public int PlayerId { get; set; }

    // 변동 대상 아이템
    public int ItemId { get; set; }

    // 변동 사유 식별자 (예: "MailClaim", "AdminGrant", "ShopPurchase")
    public string Reason { get; set; } = "";

    // 변동량 (획득 +, 소모 -)
    public int ChangeAmount { get; set; }

    // 변동 전 수량
    public int BalanceBefore { get; set; }

    // 변동 후 수량
    public int BalanceAfter { get; set; }

    // 이상치 여부 — AnomalyThreshold 초과 시 true
    public bool IsAnomaly { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
