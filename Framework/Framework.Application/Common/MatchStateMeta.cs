using Framework.Domain.Enums;

namespace Framework.Application.Common;

// MatchState별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 상태 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
public static class MatchStateMeta
{
    private record Meta(string Label);

    private static readonly Dictionary<MatchState, Meta> Registry = new()
    {
        [MatchState.Waiting]    = new("Waiting(대기 중)"),
        [MatchState.InProgress] = new("InProgress(진행 중)"),
        [MatchState.Finished]   = new("Finished(완료)"),
        [MatchState.Aborted]    = new("Aborted(비정상 종료)"),
    };

    // 등록 여부 확인 — 완전성 보장 테스트에서 사용
    public static bool IsRegistered(MatchState state) => Registry.ContainsKey(state);

    // 드롭다운용 전체 옵션 목록 — (Label, Value) 튜플 리스트
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // 상태에 해당하는 표시 레이블 반환 — 미등록 시 enum 이름 그대로 반환
    public static string GetLabel(MatchState state) =>
        Registry.TryGetValue(state, out var meta) ? meta.Label : state.ToString();
}
