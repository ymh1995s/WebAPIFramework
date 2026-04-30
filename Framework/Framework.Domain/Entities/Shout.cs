namespace Framework.Domain.Entities;

// 1회 공지 엔티티 — Admin이 전체 또는 특정 플레이어에게 전송하는 HUD 메시지
public class Shout
{
    // 기본 키
    public int Id { get; set; }

    // 대상 플레이어 ID — null이면 전체 플레이어 대상, 값이 있으면 특정 플레이어만
    public int? PlayerId { get; set; }

    // HUD에 표시할 메시지 (최대 500자)
    public string Message { get; set; } = "";

    // 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; }

    // 만료 시각 (UTC) — 이후에는 클라이언트에 반환하지 않음
    public DateTime ExpiresAt { get; set; }

    // 활성 여부 — Admin이 즉시 비활성화 가능
    public bool IsActive { get; set; } = true;

    // 네비게이션 프로퍼티
    public Player? Player { get; set; }
}
