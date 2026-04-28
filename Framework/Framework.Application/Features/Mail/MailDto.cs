namespace Framework.Application.Features.Mail;

// 우편 조회 응답 DTO
// ItemName: 아이템 없는 우편이면 null
public record MailDto(int Id, int PlayerId, string Title, string Body, int? ItemId, string? ItemName, int ItemCount, bool IsRead, bool IsClaimed, DateTime CreatedAt, DateTime ExpiresAt);

// 우편 발송 요청 DTO
// ExpiresInDays: 발송 시점 기준 만료까지 일수 (기본 30일)
public record SendMailDto(int PlayerId, string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30);

// 전체 플레이어 일괄 발송 요청 DTO
public record BulkSendMailDto(string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30);
