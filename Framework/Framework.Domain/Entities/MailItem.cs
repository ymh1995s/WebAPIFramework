namespace Framework.Domain.Entities;

// 우편 첨부 아이템 — 1통 우편에 N종 아이템을 담기 위한 테이블
// 기존 Mail.ItemId/ItemCount는 deprecated 유지, 신규 다중 아이템은 이 테이블로 관리
public class MailItem
{
    // 기본 키
    public int Id { get; set; }

    // 소속 우편 FK
    public int MailId { get; set; }

    // 아이템 마스터 FK
    public int ItemId { get; set; }

    // 지급 수량
    public int Quantity { get; set; }

    // 우편 네비게이션 프로퍼티
    public Mail Mail { get; set; } = null!;

    // 아이템 마스터 네비게이션 프로퍼티
    public Item Item { get; set; } = null!;
}
