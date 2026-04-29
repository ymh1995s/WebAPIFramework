using System.ComponentModel.DataAnnotations;

namespace Framework.Application.Features.Iap;

// 인앱결제 영수증 검증 요청 DTO — 클라이언트가 서버로 전송하는 구매 정보
public record IapVerifyRequest(
    // 스토어 상품 식별자 (SKU) — 필수값, 빈 문자열 불허
    [Required]
    [MinLength(1, ErrorMessage = "ProductId는 비어 있을 수 없습니다.")]
    string ProductId,

    // 스토어에서 발급한 구매 토큰 — Google: purchaseToken, Apple: transactionId, 필수값
    [Required]
    [MinLength(1, ErrorMessage = "PurchaseToken은 비어 있을 수 없습니다.")]
    string PurchaseToken,

    // 스토어 주문 번호 (선택값) — Google: GPA.xxxx
    string? OrderId
);
