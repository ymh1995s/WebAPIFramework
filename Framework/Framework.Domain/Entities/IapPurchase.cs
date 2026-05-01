using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 인앱결제 구매 이력 엔티티 — 플레이어의 결제 처리 전 과정을 추적
// UNIQUE(Store, PurchaseToken) — 동일 결제 토큰 중복 처리 방지
public class IapPurchase
{
    // 기본 키
    public int Id { get; set; }

    // 구매를 수행한 플레이어 FK
    public int PlayerId { get; set; }

    // 플레이어 네비게이션 프로퍼티
    public Player? Player { get; set; }

    // 스토어 종류 (Google Play / Apple App Store)
    public IapStore Store { get; set; }

    // 스토어 상품 식별자 (SKU) — 구매 시점의 ProductId 스냅샷
    public string ProductId { get; set; } = string.Empty;

    // 스토어 발급 결제 토큰 — 중복 처리 방지 및 환불 추적에 사용
    public string PurchaseToken { get; set; } = string.Empty;

    // 스토어 주문 번호 (Google: GPA.xxxx, Apple: 향후 지원)
    public string? OrderId { get; set; }

    // 현재 처리 상태 (Pending → Verified → Granted / Refunded / Failed)
    public IapPurchaseStatus Status { get; set; } = IapPurchaseStatus.Pending;

    // 보상 지급 시점의 RewardTable ID 스냅샷 — 사후 감사에 활용
    public int? RewardTableIdSnapshot { get; set; }

    // Google 검증 API 응답 원본 JSON — 분쟁/감사 대응용
    public string? RawReceipt { get; set; }

    // 스토어에서 보고한 결제 발생 시각 (UTC)
    public DateTime? PurchaseTimeUtc { get; set; }

    // 영수증 검증 완료 시각 (UTC)
    public DateTime? VerifiedAt { get; set; }

    // 보상 지급 완료 시각 (UTC)
    public DateTime? GrantedAt { get; set; }

    // 환불 처리 시각 (UTC) — RTDN 수신 또는 Admin 수동 처리
    public DateTime? RefundedAt { get; set; }

    // 환불 사유 구분 — "Voided"(Google 강제환불) / "Canceled"(정상 취소)
    public string? RefundReason { get; set; }

    // 실패 원인 메시지 — 검증 실패 시 상세 사유 기록
    public string? FailureReason { get; set; }

    // 구매 요청 클라이언트 IP — 어뷰징 탐지용
    public string? ClientIp { get; set; }

    // 레코드 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 최종 수정 시각 (UTC)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 상품 유형 스냅샷 — 구매 시점 ProductType 기록 (retry 워커의 Consumable 필터링용)
    public IapProductType? ProductType { get; set; }

    // Google Play consume API 완료 시각 (UTC) — null이면 미호출 또는 retry 대기 중
    public DateTime? ConsumedAt { get; set; }

    // consume 시도 횟수 — retry 워커의 지수 백오프 계산 기준
    public int ConsumeAttempts { get; set; } = 0;

    // 마지막 consume 시도 시각 (UTC) — 백오프 대기 시간 계산 기준점
    public DateTime? LastConsumeAttemptAt { get; set; }

    // 마지막 consume 실패 메시지 — 운영 디버깅 및 Admin 알림 참고용
    public string? LastConsumeError { get; set; }
}
