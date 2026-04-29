using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 광고 보상 정책 엔티티 — 광고 네트워크별 PlacementId에 대한 보상 규칙 정의
// UNIQUE(Network, PlacementId) WHERE !IsDeleted — 동일 광고 슬롯 중복 정책 방지
public class AdPolicy
{
    // 기본 키
    public int Id { get; set; }

    // 광고 네트워크 종류 (UnityAds, IronSource 등)
    public AdNetworkType Network { get; set; }

    // 광고 네트워크에서 관리하는 게재 위치 식별자 (예: "Rewarded_MainMenu")
    public string PlacementId { get; set; } = string.Empty;

    // 광고 게재 위치 타입 (리워드 비디오, 인터스티셜 등)
    public AdPlacementType PlacementType { get; set; }

    // 보상 테이블 FK — null이면 보상 없음 (광고 시청만 추적)
    public int? RewardTableId { get; set; }

    // 보상 테이블 네비게이션 프로퍼티 (nullable)
    public RewardTable? RewardTable { get; set; }

    // 하루 최대 보상 지급 횟수 (0이면 무제한)
    public int DailyLimit { get; set; } = 0;

    // 정책 활성화 여부 — false이면 콜백 수신 시 보상 미지급
    public bool IsEnabled { get; set; } = true;

    // 정책 설명 (Admin 표시용)
    public string Description { get; set; } = string.Empty;

    // 소프트 삭제 여부 — true이면 논리 삭제 (실제 DB 행은 유지)
    public bool IsDeleted { get; set; } = false;

    // 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 최종 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
