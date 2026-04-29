namespace Framework.Domain.Enums;

// 인앱결제 구매 처리 상태 — Pending → Verified → Granted 순으로 진행
public enum IapPurchaseStatus
{
    // 결제 요청 접수 후 검증 대기 중
    Pending = 0,

    // 스토어 영수증 검증 완료 (아직 보상 미지급)
    Verified = 1,

    // 보상 지급 완료
    Granted = 2,

    // 환불 처리됨 (RTDN 수신 또는 Admin 처리)
    Refunded = 3,

    // 검증 실패 또는 처리 오류
    Failed = 4
}
