namespace Framework.Domain.Entities;

// 일일 로그인 보상 설정 (연속일수별 보상 정의)
public class DailyRewardConfig
{
    public int Id { get; set; }
    // 연속 출석일수 (1일차, 2일차... 현재는 단일 보상이라 Day=1만 사용)
    public int Day { get; set; }
    public int ItemId { get; set; }
    public int ItemCount { get; set; }

    // EF Core 네비게이션 프로퍼티
    public Item Item { get; set; } = null!;
}
