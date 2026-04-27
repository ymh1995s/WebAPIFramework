namespace Framework.Domain.Entities;

// 플레이어 계정 엔티티 (인증 전용)
public class Player
{
    // 기본 키
    public int Id { get; set; }

    // 게스트 로그인용 기기 식별자 (UUID)
    public string DeviceId { get; set; } = string.Empty;

    // 구글 로그인 연동용 (나중에 사용)
    public string? GoogleId { get; set; }

    // 닉네임
    public string Nickname { get; set; } = string.Empty;

    // 계정 생성 일시 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 마지막 로그인 일시 (UTC)
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    // 밴 여부 (기본값: false, 정상 계정)
    public bool IsBanned { get; set; } = false;

    // 밴 해제 일시 (null이면 영구 밴, 값이 있으면 해당 시각까지 기간 밴)
    public DateTime? BannedUntil { get; set; }

    // 인게임 프로필 (1:1)
    public PlayerProfile? Profile { get; set; }

    // 리프래시 토큰 목록
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // 보유 아이템
    public ICollection<PlayerItem> Items { get; set; } = new List<PlayerItem>();

    // 우편함
    public ICollection<Mail> Mails { get; set; } = new List<Mail>();

    // 일일 로그인 기록
    public ICollection<DailyLoginLog> LoginLogs { get; set; } = new List<DailyLoginLog>();

    // 게임 플레이 기록
    public ICollection<PlayerRecord> Records { get; set; } = new List<PlayerRecord>();

    // 소원수리함 문의 목록
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
}
