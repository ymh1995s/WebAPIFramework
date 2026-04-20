namespace Framework.Domain.Entities;

// 플레이어 게임 기록 엔티티
public class PlayerRecord
{
    // 기본 키
    public int Id { get; set; }
    // 플레이어 닉네임
    public string Nickname { get; set; } = string.Empty;
    // 플레이 타임 (초 단위)
    public float PlayTime { get; set; }
    // 점수
    public int Score { get; set; }
    // 기록 생성 일시 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
