namespace Framework.Domain.Entities;

// 플레이어 보유 아이템 (인벤토리)
public class PlayerItem
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int ItemId { get; set; }
    // 동일 아이템은 행을 추가하지 않고 수량만 증가
    public int Quantity { get; set; }

    // EF Core 네비게이션 프로퍼티 (null!: EF가 채워주므로 null 경고 억제)
    public PlayerRecord Player { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
