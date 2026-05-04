namespace Framework.Domain.Entities;

// 리프래시 토큰 엔티티 (재발급 티켓)
public class RefreshToken
{
    // 기본 키
    public int Id { get; set; }

    // 토큰을 소유한 플레이어 ID (FK)
    public int PlayerId { get; set; }

    // SHA-256 해시 (Base64 인코딩, 44자 고정) — 평문 토큰은 DB에 저장하지 않음
    public string TokenHash { get; set; } = string.Empty;

    // 만료 일시 (UTC)
    public DateTime ExpiresAt { get; set; }

    // 발급 시점 클라이언트 IP — 사후 포렌식용 (IPv6 포함 최대 45자)
    public string? IpAddress { get; set; }

    // 발급 시점 User-Agent — 사후 포렌식용 (최대 512자)
    public string? UserAgent { get; set; }

    // 명시적 폐기 시각 — 강제 로그아웃 등 활용 (검증 시 IS NULL 체크)
    public DateTime? RevokedAt { get; set; }

    // 생성 일시 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
