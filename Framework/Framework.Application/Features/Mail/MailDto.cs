namespace Framework.Application.Features.Mail;

// 우편 첨부 아이템 단위 DTO — 다중 아이템 우편 응답에서 사용
// 통화 아이템(Gold Id=1, Gems Id=2)도 이 목록으로 반환됨
public record MailItemDto(int ItemId, string ItemName, int Quantity);

// 우편 조회 응답 DTO
// ItemName: 아이템 없는 우편이면 null (deprecated 단일 아이템 호환)
// Exp: 우편에 첨부된 경험치 (레거시 Mail.Exp 컬럼 — 레벨업 처리 연동)
// MailItems: 다중 아이템 첨부 목록 — 통화 아이템(Gold/Gems)도 여기에 포함됨
public record MailDto(int Id, int PlayerId, string Title, string Body, int? ItemId, string? ItemName, int ItemCount, bool IsRead, bool IsClaimed, DateTime CreatedAt, DateTime ExpiresAt, int Exp = 0, List<MailItemDto>? MailItems = null);

// 우편 발송 요청 DTO (레거시 단일 아이템 방식)
// ExpiresInDays: 발송 시점 기준 만료까지 일수 (기본 30일)
// Exp: 우편에 첨부할 경험치 (기본 0)
public record SendMailDto(int PlayerId, string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30, int Exp = 0);

// 우편 발송 아이템 항목 DTO — 다중 아이템 발송 요청 시 사용
public record SendMailItemDto(int ItemId, int Quantity);

// 전체 플레이어 일괄 발송 요청 DTO
// Items: 다중 아이템 첨부 목록 — 통화 아이템(Gold Id=1, Gems Id=2) 포함 가능
// Exp: 레거시 경험치 필드 (기본 0)
public record BulkSendMailDto(string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30, int Exp = 0, List<SendMailItemDto>? Items = null);
