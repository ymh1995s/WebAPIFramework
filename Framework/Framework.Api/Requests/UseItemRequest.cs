using System.ComponentModel.DataAnnotations;

namespace Framework.Api.Requests;

// 아이템 사용 요청 DTO
// ClientRequestId: 클라이언트가 생성한 UUID — 동일 요청의 중복 처리 방지(멱등성)용
public record UseItemRequest([Required] string ClientRequestId);
