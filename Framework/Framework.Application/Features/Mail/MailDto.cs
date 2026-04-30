namespace Framework.Application.Features.Mail;

// 우편 조회 응답 DTO
// ItemName: 아이템 없는 우편이면 null
// Gold/Gems/Exp: 우편에 첨부된 재화 (수령 시 PlayerProfile에 직접 지급)
public record MailDto(int Id, int PlayerId, string Title, string Body, int? ItemId, string? ItemName, int ItemCount, bool IsRead, bool IsClaimed, DateTime CreatedAt, DateTime ExpiresAt, int Gold = 0, int Gems = 0, int Exp = 0);

// 우편 발송 요청 DTO
// ExpiresInDays: 발송 시점 기준 만료까지 일수 (기본 30일)
// Gold/Gems/Exp: 우편에 첨부할 재화 (기본 0)
public record SendMailDto(int PlayerId, string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30, int Gold = 0, int Gems = 0, int Exp = 0);

// 전체 플레이어 일괄 발송 요청 DTO
// Gold/Gems/Exp: 우편에 첨부할 재화 (기본 0)
public record BulkSendMailDto(string Title, string Body, int? ItemId, int ItemCount, int ExpiresInDays = 30, int Gold = 0, int Gems = 0, int Exp = 0);
