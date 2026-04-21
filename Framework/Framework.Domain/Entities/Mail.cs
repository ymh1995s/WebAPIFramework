namespace Framework.Domain.Entities;

// 우편 (보상 전달 수단)
public class Mail
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    // null이면 아이템 없는 순수 텍스트 우편
    public int? ItemId { get; set; }
    public int ItemCount { get; set; }
    // 읽음 여부 (수령과 별개 - 읽었어도 수령 전일 수 있음)
    public bool IsRead { get; set; }
    // 수령 여부 (true이면 아이템이 인벤토리로 이동 완료)
    public bool IsClaimed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // 만료일 이후 우편 삭제 처리 대상
    public DateTime ExpiresAt { get; set; }

    // EF Core 네비게이션 프로퍼티 (null!: EF가 채워주므로 null 경고 억제)
    public PlayerRecord Player { get; set; } = null!;
    public Item? Item { get; set; }
}
