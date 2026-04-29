namespace Framework.Application.Features.Iap;

// Google Cloud Pub/Sub RTDN 페이로드 역직렬화용 DTO 모음
// RTDN(Real-time Developer Notifications): 환불/구매 이벤트를 Google이 서버로 푸시하는 방식

// Pub/Sub 메시지 래퍼 — POST body 최상단 구조
// Data 필드: base64 인코딩된 RTDN JSON 페이로드
public record PubSubMessage(string Data, string MessageId);

// Pub/Sub 푸시 요청 전체 구조 — 컨트롤러에서 역직렬화하는 루트 타입
public record PubSubPushRequest(PubSubMessage Message, string Subscription);

// base64 디코딩 후 역직렬화되는 RTDN 실제 페이로드
// OneTimeProductNotification, VoidedPurchaseNotification, TestNotification 중 하나만 포함됨
public record RtdnPayload(
    string Version,
    string PackageName,
    string EventTimeMillis,
    OneTimeProductNotification? OneTimeProductNotification,
    VoidedPurchaseNotification? VoidedPurchaseNotification,
    TestNotification? TestNotification
);

// 일회성 상품(소모성/비소모성) 구매/취소 알림
// NotificationType: 1=PURCHASED(구매), 2=CANCELED(취소/환불)
public record OneTimeProductNotification(
    string Version,
    int NotificationType,
    string PurchaseToken,
    string Sku
);

// 구매 무효화(환불) 알림 — Voided Purchase API 통해 강제 환불된 경우
// ProductType: 0=Test, 1=OneTime(단건), 2=Subscription(구독)
// RefundType: 0=FULL_REFUND(전액환불), 1=QUANTITY_BASED_PARTIAL_REFUND(수량기반부분환불)
public record VoidedPurchaseNotification(
    string PurchaseToken,
    string OrderId,
    int ProductType,
    int RefundType
);

// Play Console에서 수동으로 발송하는 테스트 알림 — 실제 처리 불필요, 로그만 기록
public record TestNotification(string Version);
