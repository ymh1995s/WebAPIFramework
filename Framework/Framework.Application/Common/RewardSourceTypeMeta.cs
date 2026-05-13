using Framework.Domain.Enums;

namespace Framework.Application.Common;

// SourceType별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 enum 값 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI 전체에 자동 반영
// IsRegistered 단위 테스트가 완전성을 보장하므로 누락 시 테스트에서 즉시 감지
public static class RewardSourceTypeMeta
{
    // SourceType 표시 메타데이터 레코드
    private record Meta(string Label, string CodeExample, bool HideInRewardTables = false);

    // 단일 진실 공급원 딕셔너리 — 새 enum 값 추가 시 여기에만 등록
    private static readonly Dictionary<RewardSourceType, Meta> Registry = new()
    {
        // DailyLogin은 [Obsolete]이지만 완전성 테스트 및 이력 조회를 위해 Registry에 포함해야 함
#pragma warning disable CS0618
        [RewardSourceType.DailyLogin]        = new("DailyLogin(일일 접속)",        "daily_login_day1",                    HideInRewardTables: true),
#pragma warning restore CS0618
        [RewardSourceType.MatchComplete]     = new("MatchComplete(매치 완료)",     "match_win_ranked, match_loss_casual"),
        [RewardSourceType.QuestComplete]     = new("QuestComplete(퀘스트 완료)",   "quest_001, quest_main_ch1"),
        [RewardSourceType.AchievementUnlock] = new("AchievementUnlock(업적 달성)", "ach_first_win, ach_lv50"),
        [RewardSourceType.LevelUp]           = new("LevelUp(레벨업)",              "lv_5, lv_10, lv_default"),
        [RewardSourceType.EventReward]       = new("EventReward(이벤트 보상)",     "event_2026spring_d1"),
        [RewardSourceType.AdminGrant]        = new("AdminGrant(관리자 지급)",       "admin_manual_001"),
        [RewardSourceType.AdReward]          = new("AdReward(광고 보상)",           "ad_daily_1, ad_doublegold"),
        [RewardSourceType.Purchase]          = new("Purchase(인앱결제)",            "iap_pkg_starter"),
        [RewardSourceType.StageComplete]     = new("StageComplete(스테이지 완료)",  "stage_1, stage_2, stage_boss_1"),
        [RewardSourceType.CouponCode]        = new("CouponCode(쿠폰)",             "coupon_welcome, coupon_event_2026"),
        [RewardSourceType.SeasonReward]      = new("SeasonReward(시즌 보상)",       "season_2026_s1, season_ranked_gold"),
        [RewardSourceType.ItemUse]           = new("ItemUse(아이템 사용)",          "item_use_consume"),
        [RewardSourceType.ShopPurchase]      = new("ShopPurchase(상점 구매)",       "shop_starter_pack"),
    };

    // 단위 테스트에서 모든 enum 값의 등록 여부를 확인하는 데 사용
    public static bool IsRegistered(RewardSourceType type) => Registry.ContainsKey(type);

    // RewardTables UI 드롭다운 — HideInRewardTables=true 항목(DailyLogin) 제외
    public static IReadOnlyList<(string Label, int Value)> RewardTablesOptions { get; } =
        Registry.Where(kv => !kv.Value.HideInRewardTables)
                .Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // RewardGrants UI 드롭다운 — 이력 조회이므로 DailyLogin 포함 전체 표시
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // 서버 camelCase SourceType 문자열 → 한글 포함 레이블 변환 (목록 테이블 표시용)
    public static string GetLabel(string camelCaseSourceType)
    {
        if (Enum.TryParse<RewardSourceType>(camelCaseSourceType, ignoreCase: true, out var e)
            && Registry.TryGetValue(e, out var meta))
            return meta.Label;
        // 매핑 없으면 첫 글자만 대문자로 fallback
        return string.IsNullOrEmpty(camelCaseSourceType)
            ? camelCaseSourceType
            : char.ToUpperInvariant(camelCaseSourceType[0]) + camelCaseSourceType[1..];
    }

    // int 값으로 CodeExample 조회 (생성 모달 힌트 문구 표시용)
    public static string GetCodeExample(int intValue)
    {
        var key = (RewardSourceType)intValue;
        return Registry.TryGetValue(key, out var meta) ? meta.CodeExample : string.Empty;
    }
}
