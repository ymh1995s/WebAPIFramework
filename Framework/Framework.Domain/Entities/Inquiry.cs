namespace Framework.Domain.Entities;

// 소원수리함 — 플레이어가 개발자에게 자유롭게 남기는 메시지
public class Inquiry
{
    public int Id { get; set; }

    // 문의를 남긴 플레이어 FK
    public int PlayerId { get; set; }

    // 자유 형식 문의 내용
    public string Content { get; set; } = "";

    // 개발자 답변 (null이면 미답변)
    public string? AdminReply { get; set; }

    // 답변 등록 시각
    public DateTime? RepliedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
