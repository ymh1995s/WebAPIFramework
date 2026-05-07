using System.ComponentModel.DataAnnotations;
using Framework.Domain.Entities;

namespace Framework.Application.Features.Inquiry;

// 플레이어 → 개발자 문의 제출 요청 — Content 최대 길이는 Inquiry.ContentMaxLength 상수 참조
public record SubmitInquiryDto(
    [Required]
    [MinLength(1, ErrorMessage = "문의 내용을 입력해주세요.")]
    [MaxLength(Framework.Domain.Entities.Inquiry.ContentMaxLength, ErrorMessage = "문의 내용은 2000자를 초과할 수 없습니다.")]
    string Content
);

// 개발자 → 문의 답변 요청 (Admin) — Reply 최대 길이는 Inquiry.AdminReplyMaxLength 상수 참조
public record ReplyInquiryDto(
    [Required]
    [MinLength(1, ErrorMessage = "답변 내용을 입력해주세요.")]
    [MaxLength(Framework.Domain.Entities.Inquiry.AdminReplyMaxLength, ErrorMessage = "답변 내용은 4000자를 초과할 수 없습니다.")]
    string Reply
);

// 플레이어에게 반환하는 문의 응답
public record InquiryDto(int Id, string Content, string? AdminReply, DateTime? RepliedAt, DateTime CreatedAt);

// Admin에게 반환하는 문의 응답 (플레이어 정보 포함)
public record InquiryAdminDto(int Id, int PlayerId, string PlayerNickname, string Content, string? AdminReply, DateTime? RepliedAt, DateTime CreatedAt);
