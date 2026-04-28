namespace Framework.Application.Features.Inquiry;

// 플레이어 → 개발자 문의 제출 요청
public record SubmitInquiryDto(string Content);

// 개발자 → 문의 답변 요청 (Admin)
public record ReplyInquiryDto(string Reply);

// 플레이어에게 반환하는 문의 응답
public record InquiryDto(int Id, string Content, string? AdminReply, DateTime? RepliedAt, DateTime CreatedAt);

// Admin에게 반환하는 문의 응답 (플레이어 정보 포함)
public record InquiryAdminDto(int Id, int PlayerId, string PlayerNickname, string Content, string? AdminReply, DateTime? RepliedAt, DateTime CreatedAt);
