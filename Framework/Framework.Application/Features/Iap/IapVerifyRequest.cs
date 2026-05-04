using System.ComponentModel.DataAnnotations;

namespace Framework.Application.Features.Iap;

// 인앱결제 영수증 검증 요청 DTO — 클라이언트가 서버로 전송하는 구매 정보
// MaxLength 값은 AppDbContext의 IapPurchase 컬럼 매핑과 동기화 (M-50)
//   ProductId      → HasMaxLength(128)
//   PurchaseToken  → HasMaxLength(512)
//   OrderId        → HasMaxLength(128)
public record IapVerifyRequest(
    // 스토어 상품 식별자 (SKU) — 필수값, DB 컬럼 길이(128)와 동기화
    [Required]
    [MinLength(1, ErrorMessage = "ProductId는 비어 있을 수 없습니다.")]
    [MaxLength(128, ErrorMessage = "ProductId는 128자를 초과할 수 없습니다.")]
    string ProductId,

    // 스토어에서 발급한 구매 토큰 — Google: purchaseToken, Apple: transactionId
    // 필수값, DB 컬럼 길이(512)와 동기화
    [Required]
    [MinLength(1, ErrorMessage = "PurchaseToken은 비어 있을 수 없습니다.")]
    [MaxLength(512, ErrorMessage = "PurchaseToken은 512자를 초과할 수 없습니다.")]
    string PurchaseToken,

    // 스토어 주문 번호 (선택값) — Google: GPA.xxxx, DB 컬럼 길이(128)와 동기화
    [MaxLength(128, ErrorMessage = "OrderId는 128자를 초과할 수 없습니다.")]
    string? OrderId
);
