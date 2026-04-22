namespace Framework.Domain.Entities;

// 리프래시 토큰 엔티티 (재발급 티켓)
public class RefreshToken
{
    // 기본 키
    public int Id { get; set; }

    // 토큰을 소유한 플레이어 ID (FK)
    public int PlayerId { get; set; }

    // 토큰 문자열
    public string Token { get; set; } = string.Empty;

    // 만료 일시 (UTC)
    public DateTime ExpiresAt { get; set; }

    // 생성 일시 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
