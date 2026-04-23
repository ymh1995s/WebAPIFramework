using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

// 문의 서비스 구현체
public class InquiryService : IInquiryService
{
    private readonly IInquiryRepository _inquiryRepository;

    public InquiryService(IInquiryRepository inquiryRepository)
        => _inquiryRepository = inquiryRepository;

    // 문의 제출 — JWT에서 추출한 PlayerId로 저장
    public async Task SubmitAsync(int playerId, SubmitInquiryDto dto)
    {
        var inquiry = new Inquiry
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

    // 답변 등록 — 기존 답변도 덮어쓰기 가능
    public async Task<bool> ReplyAsync(int inquiryId, ReplyInquiryDto dto)
    {
        var inquiry = await _inquiryRepository.GetByIdAsync(inquiryId);
        if (inquiry is null) return false;

        inquiry.AdminReply = dto.Reply;
        inquiry.RepliedAt = DateTime.UtcNow;
        await _inquiryRepository.SaveChangesAsync();
        return true;
    }
}
