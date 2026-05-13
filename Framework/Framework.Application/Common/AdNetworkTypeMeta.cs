using Framework.Domain.Enums;

namespace Framework.Application.Common;

// AdNetworkType별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 네트워크 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
public static class AdNetworkTypeMeta
{
    private record Meta(string Label);

    private static readonly Dictionary<AdNetworkType, Meta> Registry = new()
    {
        [AdNetworkType.UnityAds]   = new("UnityAds(유니티 광고)"),
        [AdNetworkType.IronSource] = new("IronSource(아이언소스)"),
    };

    // 등록 여부 확인 — 완전성 보장 테스트에서 사용
    public static bool IsRegistered(AdNetworkType network) => Registry.ContainsKey(network);

    // 드롭다운용 전체 옵션 목록 — (Label, Value) 튜플 리스트
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // 네트워크 타입에 해당하는 표시 레이블 반환 — 미등록 시 enum 이름 그대로 반환
    public static string GetLabel(AdNetworkType network) =>
        Registry.TryGetValue(network, out var meta) ? meta.Label : network.ToString();
}
