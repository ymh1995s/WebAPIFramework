// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

namespace Framework.Domain.Content.Entities;

// 스테이지 마스터 데이터 엔티티 — 스테이지 클리어의 규칙과 보상을 정의
public class Stage
{
    // 기본 키
    public int Id { get; set; }

    // 스테이지 코드 — UNIQUE, 게임 클라이언트가 참조하는 식별자
    public string Code { get; set; } = string.Empty;

    // 스테이지 이름
    public string Name { get; set; } = string.Empty;

    // 최초 클리어 보상 테이블 코드 (null이면 최초 클리어 보상 없음)
    public string? RewardTableCode { get; set; }

    // 재클리어 보상 테이블 코드 (null이면 재클리어 보상 없음)
    public string? RePlayRewardTableCode { get; set; }

    // 재클리어 횟수당 보상 감소율(%) — 0이면 감소 없이 동일 보상
    // 예: 10이면 2번째 클리어부터 10%씩 감소, 최소 50% 보장
    public int RePlayRewardDecayPercent { get; set; } = 0;

    // 클리어 시 지급되는 경험치
    public int ExpReward { get; set; } = 0;

    // 순차 진행 조건 — 이전에 클리어해야 하는 스테이지 ID (null이면 조건 없음)
    public int? RequiredPrevStageId { get; set; }

    // 스테이지 활성화 여부 — false이면 클라이언트에서 표시되지 않고 클리어 불가
    public bool IsActive { get; set; } = true;

    // 정렬 순서 — 클라이언트 UI 표시 순서
    public int SortOrder { get; set; } = 0;
}
