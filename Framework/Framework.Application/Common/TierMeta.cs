using Framework.Domain.Enums;

namespace Framework.Application.Common;

// Tier별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 티어 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
public static class TierMeta
{
    private record Meta(string Label);

    private static readonly Dictionary<Tier, Meta> Registry = new()
    {
        [Tier.Tier1]  = new("Tier1(티어1)"),
        [Tier.Tier2]  = new("Tier2(티어2)"),
        [Tier.Tier3]  = new("Tier3(티어3)"),
        [Tier.Tier4]  = new("Tier4(티어4)"),
        [Tier.Tier5]  = new("Tier5(티어5)"),
        [Tier.Tier6]  = new("Tier6(티어6)"),
        [Tier.Tier7]  = new("Tier7(티어7)"),
        [Tier.Tier8]  = new("Tier8(티어8)"),
        [Tier.Tier9]  = new("Tier9(티어9)"),
        [Tier.Tier10] = new("Tier10(티어10)"),
    };

    // 등록 여부 확인 — 완전성 보장 테스트에서 사용
    public static bool IsRegistered(Tier tier) => Registry.ContainsKey(tier);

    // 드롭다운용 전체 옵션 목록 — (Label, Value) 튜플 리스트
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // 티어에 해당하는 표시 레이블 반환 — 미등록 시 enum 이름 그대로 반환
    public static string GetLabel(Tier tier) =>
        Registry.TryGetValue(tier, out var meta) ? meta.Label : tier.ToString();
}
