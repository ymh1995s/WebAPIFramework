using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Inquiry;

// 문의 서비스 구현체
public class InquiryService : IInquiryService
{
    private readonly IInquiryRepository _inquiryRepository;

    public InquiryService(IInquiryRepository inquiryRepository)
        => _inquiryRepository = inquiryRepository;

    // 문의 제출 — JWT에서 추출한 PlayerId로 저장, 공백 차단 및 길이 초과 시 예외
    public async Task SubmitAsync(int playerId, SubmitInquiryDto dto)
    {
        // 공백 문자열 차단 — Trim 후 빈 문자열이면 예외
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("문의 내용을 입력해주세요.");

        // DTO DataAnnotations 검증은 컨트롤러에서 처리되지만, 서비스 방어선도 유지
        if (dto.Content.Length > Framework.Domain.Entities.Inquiry.ContentMaxLength)
            throw new ArgumentException("문의 내용은 2000자를 초과할 수 없습니다.", nameof(dto.Content));

        var inquiry = new Domain.Entities.Inquiry
        {
            PlayerId = playerId,
            Content = dto.Content
        };
        await _inquiryRepository.AddAsync(inquiry);
        await _inquiryRepository.SaveChangesAsync();
    }

    // 내 문의 목록 조회
    public async Task<List<InquiryDto>> GetMyInquiriesAsync(int playerId)
    {
        var inquiries = await _inquiryRepository.GetByPlayerIdAsync(playerId);
        return inquiries.Select(i => new InquiryDto(
            i.Id, i.Content, i.AdminReply, i.RepliedAt, i.CreatedAt
        )).ToList();
    }

    // 전체 문의 목록 조회 (Admin)
    public async Task<List<InquiryAdminDto>> GetAllAsync()
    {
        var inquiries = await _inquiryRepository.GetAllAsync();
        return inquiries.Select(i => new InquiryAdminDto(
            i.Id, i.PlayerId, i.Player.Nickname, i.Content, i.AdminReply, i.RepliedAt, i.CreatedAt
        )).ToList();
    }

    // 답변 등록 — 기존 답변도 덮어쓰기 가능, 공백 차단 및 길이 초과 시 예외
    public async Task<bool> ReplyAsync(int inquiryId, ReplyInquiryDto dto)
    {
        // 공백 문자열 차단 — Trim 후 빈 문자열이면 예외
        if (string.IsNullOrWhiteSpace(dto.Reply))
            throw new ArgumentException("답변 내용을 입력해주세요.");

        // 답변 길이 서비스 방어선
        if (dto.Reply.Length > Framework.Domain.Entities.Inquiry.AdminReplyMaxLength)
            throw new ArgumentException("답변 내용은 4000자를 초과할 수 없습니다.", nameof(dto.Reply));

        var inquiry = await _inquiryRepository.GetByIdAsync(inquiryId);
        if (inquiry is null) return false;

        inquiry.AdminReply = dto.Reply;
        inquiry.RepliedAt = DateTime.UtcNow;
        await _inquiryRepository.SaveChangesAsync();
        return true;
    }
}
