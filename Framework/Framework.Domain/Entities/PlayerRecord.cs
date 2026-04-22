namespace Framework.Domain.Entities;

// 플레이어 게임 기록 엔티티
public class PlayerRecord
{
    // 기본 키
    public int Id { get; set; }

    // 플레이어 계정 FK
    public int PlayerId { get; set; }

    // 플레이 타임 (초 단위)
    public float PlayTime { get; set; }

    // 점수
    public int Score { get; set; }

    // 기록 생성 일시 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
