namespace Framework.Domain.Constants;

// AuditLog.Reason 컬럼 표준 값 — DB 저장값/배지 매핑 동시 의존 (변경 금지)
public static class AuditLogReasons
{
    // 우편 수령을 통한 아이템 획득
    public const string MailClaim = "MailClaim";

    // 향후 확장 예정 — 도메인 주석 기준 미구현
    // public const string AdminGrant = "AdminGrant";
    // public const string ShopPurchase = "ShopPurchase";
}
