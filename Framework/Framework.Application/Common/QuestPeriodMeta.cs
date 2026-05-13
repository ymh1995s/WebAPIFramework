using Framework.Domain.Enums;

namespace Framework.Application.Common;

// QuestPeriod별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 주기 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
public static class QuestPeriodMeta
{
    // 주기 표시 메타데이터 레코드
    private record Meta(string Label);

    // 단일 진실 공급원 딕셔너리 — 새 enum 값 추가 시 여기에만 등록
    private static readonly Dictionary<QuestPeriod, Meta> Registry = new()
    {
        [QuestPeriod.Daily]     = new("Daily(일일)"),
        [QuestPeriod.Weekly]    = new("Weekly(주간)"),
        [QuestPeriod.Permanent] = new("Permanent(영구/메인)"),
    };

    // 단위 테스트에서 모든 enum 값의 등록 여부를 확인하는 데 사용
    public static bool IsRegistered(QuestPeriod period) => Registry.ContainsKey(period);

    // 드롭다운용 전체 목록 — int value 기준
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // enum 값 → 한글 포함 레이블 변환
    public static string GetLabel(QuestPeriod period) =>
        Registry.TryGetValue(period, out var meta) ? meta.Label : period.ToString();
}
