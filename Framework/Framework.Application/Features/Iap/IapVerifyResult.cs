namespace Framework.Application.Features.Iap;

// 인앱결제 영수증 검증 처리 결과 DTO
public record IapVerifyResult(
    // 검증 및 보상 처리 성공 여부
    bool Ok,

    // 이미 보상이 지급된 구매인지 여부 — 클라이언트 재시도 대응
    bool AlreadyGranted,

    // 생성 또는 조회된 구매 이력 ID
    int PurchaseId
);
