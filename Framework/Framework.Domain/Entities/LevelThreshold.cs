namespace Framework.Domain.Entities;

// 레벨 임계값 마스터 엔티티 — 레벨별 누적 경험치 기준을 DB에서 관리
public class LevelThreshold
{
    // 레벨 번호 — PK, 1부터 시작
    public int Level { get; set; }

    // 해당 레벨 도달에 필요한 누적 경험치
    public int RequiredExp { get; set; }

    // 마지막 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; }
}
