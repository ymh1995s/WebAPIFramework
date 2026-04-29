using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 스토어 영수증 검증 결과 값 객체 — 검증 성공 시 반환되는 정규화된 구매 정보
public record IapReceiptVerified(
    // 스토어에서 확인된 상품 ID (SKU)
    string ProductId,

    // 스토어에서 확인된 상품 유형 (소모성/비소모성)
    IapProductType ProductType,

    // 스토어에서 보고한 결제 발생 시각 (UTC)
    DateTime PurchaseTimeUtc,

    // 스토어 API 응답 원본 JSON — 분쟁/감사 대응용 보존
    string RawReceiptJson
);
