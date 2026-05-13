namespace Framework.Domain.Entities;

// 플레이어별 퀘스트 진행 상태 — 주기(PeriodKey)별로 독립 관리
// [동시성] xmin shadow property로 낙관적 동시성 제어 (PlayerItem.xmin 패턴 동일)
public class PlayerQuestProgress
{
    // 진행 상태 고유 ID (PK, bigint)
    public long Id { get; set; }

    // 플레이어 FK → Players (Cascade)
    public int PlayerId { get; set; }

    // 퀘스트 정의 FK → QuestDefinitions (Restrict)
    public int QuestId { get; set; }

    // 퀘스트 주기 키 — Daily: "yyyy-MM-dd", Weekly: "yyyy-Www", Permanent: "permanent"
    public string PeriodKey { get; set; } = "";

    // 현재 달성 수량
    public int CurrentAmount { get; set; }

    // 보상 수령 여부
    public bool IsClaimed { get; set; }

    // 보상 수령 일시 (UTC) — null이면 미수령
    public DateTimeOffset? ClaimedAt { get; set; }

    // 주기 리셋 일시 (UTC) — null이면 리셋 이력 없음
    public DateTimeOffset? ResetAt { get; set; }

    // 생성 일시 (UTC)
    public DateTimeOffset CreatedAt { get; set; }

    // 최종 수정 일시 (UTC)
    public DateTimeOffset UpdatedAt { get; set; }

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;

    // 퀘스트 정의 네비게이션 프로퍼티
    public QuestDefinition Quest { get; set; } = null!;
}
