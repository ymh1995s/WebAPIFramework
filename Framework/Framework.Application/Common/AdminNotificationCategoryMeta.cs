using Framework.Domain.Enums;

namespace Framework.Application.Common;

// AdminNotificationCategory별 Admin UI 메타데이터 레지스트리 — 단일 진실 공급원
// 새 카테고리 추가 시 이 파일 Registry에만 항목을 추가하면 Admin UI에 자동 반영
// IsRegistered 단위 테스트가 완전성을 보장 — 누락 시 즉시 감지
public static class AdminNotificationCategoryMeta
{
    // 카테고리 표시 메타데이터 레코드
    private record Meta(string Label);

    // 단일 진실 공급원 딕셔너리 — 새 enum 값 추가 시 여기에만 등록
    private static readonly Dictionary<AdminNotificationCategory, Meta> Registry = new()
    {
        [AdminNotificationCategory.IapClawback]                   = new("IapClawback(IAP 환불)"),
        [AdminNotificationCategory.IapConsumeFailure]             = new("IapConsumeFailure(IAP Consume 실패)"),
        [AdminNotificationCategory.RewardDispatchFailure]         = new("RewardDispatchFailure(보상 지급 실패)"),
        [AdminNotificationCategory.IapVerifyConcurrencyExhausted] = new("IapVerifyConcurrencyExhausted(IAP 검증 동시성 초과)"),
        [AdminNotificationCategory.BackgroundServiceFailure]      = new("BackgroundServiceFailure(백그라운드 서비스 장애)"),
    };

    // 단위 테스트에서 모든 enum 값의 등록 여부를 확인하는 데 사용
    public static bool IsRegistered(AdminNotificationCategory category) => Registry.ContainsKey(category);

    // 필터 드롭다운용 전체 목록
    public static IReadOnlyList<(string Label, int Value)> AllOptions { get; } =
        Registry.Select(kv => (kv.Value.Label, (int)kv.Key))
                .ToList();

    // enum 값 → 한글 포함 레이블 변환 (목록 테이블 표시용)
    public static string GetLabel(AdminNotificationCategory category) =>
        Registry.TryGetValue(category, out var meta) ? meta.Label : category.ToString();
}
