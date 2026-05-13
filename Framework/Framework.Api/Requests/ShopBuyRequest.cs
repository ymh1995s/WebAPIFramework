using System.ComponentModel.DataAnnotations;

namespace Framework.Api.Requests;

// 인게임 상점 구매 요청 DTO
// ClientRequestId: 클라이언트가 생성한 UUID — 동일 구매 요청의 중복 처리 방지(멱등성)용
// Quantity: 구매 수량 (기본값 1, 최소 1) — N개 구매 지원
public record ShopBuyRequest(
    [Required] string ClientRequestId,
    [Range(1, int.MaxValue)] int Quantity = 1
);
