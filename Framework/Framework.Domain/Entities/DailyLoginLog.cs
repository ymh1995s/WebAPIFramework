namespace Framework.Domain.Entities;

// 일일 로그인 기록 (중복 수령 방지)
public class DailyLoginLog
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public DateOnly LoginDate { get; set; }

    public PlayerRecord Player { get; set; } = null!;
}
