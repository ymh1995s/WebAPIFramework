using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 인앱결제 상품 마스터 엔티티 — 스토어 SKU와 보상 규칙을 연결하는 상품 정의
// UNIQUE(Store, ProductId) WHERE IsDeleted=false — 동일 SKU 중복 정책 방지
public class IapProduct
{
    // 기본 키
    public int Id { get; set; }

    // 스토어 종류 (Google Play / Apple App Store)
    public IapStore Store { get; set; }

    // 스토어에서 관리하는 상품 식별자 (SKU) — 예: "com.example.game.gold_100"
    public string ProductId { get; set; } = string.Empty;

    // 상품 유형 (소모성 / 비소모성)
    public IapProductType ProductType { get; set; }

    // 보상 테이블 FK — null이면 보상 없음 (별도 로직으로 처리)
    public int? RewardTableId { get; set; }

    // 보상 테이블 네비게이션 프로퍼티 (nullable)
    public RewardTable? RewardTable { get; set; }

    // 상품 설명 (Admin 표시용)
    public string Description { get; set; } = string.Empty;

    // 상품 활성화 여부 — false이면 구매 요청 거부
    public bool IsEnabled { get; set; } = true;

    // 소프트 삭제 여부 — true이면 논리 삭제 (실제 DB 행은 유지)
    public bool IsDeleted { get; set; } = false;

    // 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 최종 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
