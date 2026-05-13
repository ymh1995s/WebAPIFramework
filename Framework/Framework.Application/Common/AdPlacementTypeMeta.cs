using Framework.Domain.Enums;

namespace Framework.Application.Common;

// AdPlacementType별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 게재 위치 타입 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
public static class AdPlacementTypeMeta
{
    private record Meta(string Label);

    private static readonly Dictionary<AdPlacementType, Meta> Registry = new()
    {
        [AdPlacementType.RewardedVideo] = new("RewardedVideo(리워드 비디오)"),
        [AdPlacementType.Interstitial]  = new("Interstitial(전면 광고)"),
    };

    // 등록 여부 확인 — 완전성 보장 테스트에서 사용
    public static bool IsRegistered(AdPlacementType type) => Registry.ContainsKey(type);

    // 드롭다운용 전체 옵션 목록 — (Label, Value) 튜플 리스트
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // 게재 위치 타입에 해당하는 표시 레이블 반환 — 미등록 시 enum 이름 그대로 반환
    public static string GetLabel(AdPlacementType type) =>
        Registry.TryGetValue(type, out var meta) ? meta.Label : type.ToString();
}
