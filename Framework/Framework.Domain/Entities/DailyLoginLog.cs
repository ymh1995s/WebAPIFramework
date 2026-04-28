namespace Framework.Domain.Entities;

// 일일 로그인 기록 (중복 수령 방지)
public class DailyLoginLog
{
    public int Id { get; set; }

    // 플레이어 계정 FK
    public int PlayerId { get; set; }

    public DateOnly LoginDate { get; set; }

    // 이 로그에서 지급된 사이클 일자 (1~28) — 중복 지급 방지 및 이력 추적용
    public int RewardDay { get; set; } = 1;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
