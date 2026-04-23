namespace Framework.Domain.Entities;

// 1회성 공지 — 클라이언트가 NoticeId를 로컬에 저장해 중복 표시 방지
public class Notice
{
    public int Id { get; set; }
    public string Content { get; set; } = "";

    // false이면 클라이언트에 노출되지 않음
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 수정된 적 없으면 null
    public DateTime? UpdatedAt { get; set; }
}
