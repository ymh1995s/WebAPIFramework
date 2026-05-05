namespace Framework.Application.Common;

// 보상 멱등성 SourceKey 빌더 — RewardGrants UNIQUE(PlayerId, SourceType, SourceKey) 멱등 보장 (M-21)
// 형식 변경 시 본 클래스만 수정하면 모든 사용처 일관성 유지
public static class SourceKeys
{
    // 레벨업 보상 (RewardSourceType.LevelUp)
    public static string LevelUp(int level) => $"levelup:{level}";

    // 우편 수령 (Exp 가산 시 ExpService 위임)
    public static string Mail(int mailId) => $"mail:{mailId}";

    // 일일 로그인 — 게임 날짜 기반
    public static string DailyLogin(DateOnly gameDate) => $"daily-login:{gameDate:yyyy-MM-dd}";

    // 광고 SSV — {network}:{placementId}:{transactionId}
    public static string AdReward(string network, string placementId, string transactionId)
        => $"{network}:{placementId}:{transactionId}";

    // IAP 결제 — 스토어명 prefix + purchaseToken
    public static string IapPurchase(string store, string purchaseToken)
        => $"{store}:{purchaseToken}";

    // 스테이지 최초 클리어
    public static string StageFirstClear(int stageId) => $"stage:{stageId}:first";

    // 스테이지 재클리어 (clearCount 포함하여 매 회 고유)
    public static string StageReplay(int stageId, int clearCount)
        => $"stage:{stageId}:replay:{clearCount}";

    // 스테이지 Exp 지급 (재클리어/최초 무관 단일 키)
    public static string StageExp(int stageId) => $"stage:{stageId}";
}
