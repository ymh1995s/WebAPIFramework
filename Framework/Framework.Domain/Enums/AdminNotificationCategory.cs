namespace Framework.Domain.Enums;

// Admin 알림 분류 — 카테고리별 필터링 및 향후 확장 지점
public enum AdminNotificationCategory
{
    // 인앱결제 환불/clawback
    IapClawback = 1,

    // 소모성 상품 consume API 최대 재시도 초과 — 수동 처리 필요
    IapConsumeFailure = 2,

    // 보상 지급 실패 — 일일 로그인 등 자동 보상 파이프라인에서 예외 발생 시 수동 처리 필요
    RewardDispatchFailure = 3,

    // IAP verify 동시성 충돌 한도(3회) 초과 — 재무 영향 가능, 즉시 확인 필요
    IapVerifyConcurrencyExhausted = 4,

    // 백그라운드 서비스 장기 실패 — PII 정리 등 서비스가 임계값 초과 동안 미실행 시 발송
    BackgroundServiceFailure = 5,
}
