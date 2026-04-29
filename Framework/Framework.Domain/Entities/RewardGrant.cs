using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 보상 지급 이력 — 멱등성 보장용 테이블
// UNIQUE(PlayerId, SourceType, SourceKey) 제약으로 동일 보상 중복 지급 방지
public class RewardGrant
{
    // 기본 키
    public int Id { get; set; }

    // 보상을 받은 플레이어 FK
    public int PlayerId { get; set; }

    // 보상 원천 타입 (DailyLogin, MatchComplete 등)
    public RewardSourceType SourceType { get; set; }

    // 보상 원천 식별 키 (예: "2026-04-29", "match:guid", "levelup:5")
    public string SourceKey { get; set; } = string.Empty;

    // 보상 지급 일시 (UTC)
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    // 우편 지급 시 연결된 Mail ID (Direct 지급이면 null)
    public int? MailId { get; set; }

    // 지급된 번들 스냅샷 (JSON 직렬화 — 추후 감사/환불 근거)
    public string BundleSnapshot { get; set; } = "{}";

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;

    // 우편 네비게이션 프로퍼티 (우편 지급 시에만 유효)
    public Mail? Mail { get; set; }
}
