namespace Framework.Domain.Enums;

// Admin 알림 분류 — 카테고리별 필터링 및 향후 확장 지점
public enum AdminNotificationCategory
{
    // 인앱결제 환불/clawback
    IapClawback = 1,

    // 소모성 상품 consume API 최대 재시도 초과 — 수동 처리 필요
    IapConsumeFailure = 2,
}
