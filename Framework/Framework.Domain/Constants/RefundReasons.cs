namespace Framework.Domain.Constants;

// IapPurchase.RefundReason 컬럼 표준 값 — 변경 시 기존 DB 데이터 호환 깨짐 (변경 금지)
public static class RefundReasons
{
    // Google Voided Purchase API를 통한 강제 환불
    public const string Voided = "Voided";

    // OneTimeProduct CANCELED 알림으로 인한 취소 환불
    public const string Canceled = "Canceled";
}
