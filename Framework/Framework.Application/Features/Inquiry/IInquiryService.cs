namespace Framework.Application.Features.Inquiry;

// 문의 서비스 인터페이스
public interface IInquiryService
{
    // 플레이어가 문의 제출
    Task SubmitAsync(int playerId, SubmitInquiryDto dto);

    // 플레이어가 자신의 문의 목록 조회
    Task<List<InquiryDto>> GetMyInquiriesAsync(int playerId);

    // Admin이 전체 문의 목록 조회
    Task<List<InquiryAdminDto>> GetAllAsync();

    // Admin이 문의에 답변 등록
    Task<bool> ReplyAsync(int inquiryId, ReplyInquiryDto dto);
}
