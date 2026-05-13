using Framework.Domain.Enums;

namespace Framework.Application.Common;

// QuestConditionType별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// RequiresTarget: false인 타입(Login)은 조건 대상 ID 드롭다운을 숨긴다
public static class QuestConditionTypeMeta
{
    // 조건 타입 표시 메타데이터 레코드
    // RequiresTarget: false이면 조건 대상 ID 입력란을 숨긴다 (Login 등)
    private record Meta(string Label, bool RequiresTarget = true);

    // 단일 진실 공급원 딕셔너리 — 새 enum 값 추가 시 여기에만 등록
    private static readonly Dictionary<QuestConditionType, Meta> Registry = new()
    {
        [QuestConditionType.StageCleared]  = new("StageCleared(스테이지 클리어)"),
        [QuestConditionType.ItemUsed]      = new("ItemUsed(아이템 사용)"),
        [QuestConditionType.ShopPurchased] = new("ShopPurchased(상점 구매)"),
        [QuestConditionType.Login]         = new("Login(로그인)", RequiresTarget: false),
    };

    // 단위 테스트에서 모든 enum 값의 등록 여부를 확인하는 데 사용
    public static bool IsRegistered(QuestConditionType type) => Registry.ContainsKey(type);

    // 드롭다운용 전체 목록 — int value 기준
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // enum 값 → 한글 포함 레이블 변환
    public static string GetLabel(QuestConditionType type) =>
        Registry.TryGetValue(type, out var meta) ? meta.Label : type.ToString();

    // 해당 조건 타입이 대상 ID 입력을 필요로 하는지 반환
    public static bool RequiresTarget(QuestConditionType type) =>
        Registry.TryGetValue(type, out var meta) && meta.RequiresTarget;
}
