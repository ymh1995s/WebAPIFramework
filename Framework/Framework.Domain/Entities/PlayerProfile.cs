namespace Framework.Domain.Entities;

// 플레이어 인게임 프로필 (인증과 분리된 게임 데이터)
public class PlayerProfile
{
    // 기본 키
    public int Id { get; set; }

    // 플레이어 계정 FK (1:1)
    public int PlayerId { get; set; }

    // 레벨
    public int Level { get; set; } = 1;

    // 경험치
    public int Exp { get; set; } = 0;

    // 마지막 갱신 일시 (UTC)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
