namespace Framework.Application.Common;

// AdminNotification DedupKey 빌더 — kebab-case 컨벤션 통일 (M-20)
// DB UNIQUE(DedupKey) 멱등성 보장 + 카테고리별 형식 표준화
public static class AdminNotificationDedupKeys
{
    // IAP consume API 재시도 한도 초과
    public static string IapConsumeFail(long iapPurchaseId)
        => $"iap-consume-fail:{iapPurchaseId}";

    // IAP RTDN 환불 감지 — Voided
    public static string IapRefund(string store, string purchaseToken)
        => $"iap-refund:{store}:{purchaseToken}";

    // IAP RTDN 취소 환불 감지 — Canceled
    public static string IapCancel(string store, string purchaseToken)
        => $"iap-cancel:{store}:{purchaseToken}";

    // 일일 로그인 자동 보상 파이프라인 실패
    public static string DailyLoginFail(int playerId, DateOnly gameDate)
        => $"daily-login-fail:{playerId}:{gameDate:yyyy-MM-dd}";
}
