namespace Framework.Domain.Entities;

// 인게임 상점 상품 마스터 엔티티
// Gold/Gems 등 인게임 재화로 구매 가능한 상품 정의
// 가격: 차감할 재화(ItemId) + 수량 / 판매 상품: 지급할 RewardTable
public class ShopProduct
{
    // 기본 키
    public int Id { get; set; }

    // 상품 이름 (Admin 표시용)
    public string Name { get; set; } = string.Empty;

    // 상품 설명 (Admin 표시용)
    public string Description { get; set; } = string.Empty;

    // 가격 — 차감할 통화 아이템 ID
    public int PriceItemId { get; set; }

    // 가격 — 차감할 재화 수량
    public int PriceAmount { get; set; }

    // 판매 상품 — 구매 시 지급할 보상 테이블 ID (IRewardDispatcher 연동)
    public int RewardTableId { get; set; }

    // 일일 구매 한도 — 0이면 무제한
    public int DailyLimit { get; set; } = 0;

    // 총 구매 한도 — 0이면 무제한
    public int TotalLimit { get; set; } = 0;

    // 활성화 여부 — false이면 구매 요청 거부
    public bool IsEnabled { get; set; } = true;

    // 1회 구매 최대 수량 — null이면 무제한, 설정 시 요청 수량이 이 값을 초과하면 거부
    public int? MaxPerCall { get; set; } = null;

    // 소프트 삭제 여부 — true이면 논리 삭제 (실제 DB 행은 유지)
    public bool IsDeleted { get; set; } = false;

    // 정렬 순서 — 클라이언트 상점 화면 노출 순서
    public int SortOrder { get; set; } = 0;

    // 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 최종 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 가격 재화 아이템 네비게이션 프로퍼티
    public Item PriceItem { get; set; } = null!;

    // 보상 테이블 네비게이션 프로퍼티
    public RewardTable RewardTable { get; set; } = null!;
}
