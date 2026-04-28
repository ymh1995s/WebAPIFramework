using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Notice;

// 공지 서비스 구현체
public class NoticeService : INoticeService
{
    private readonly INoticeRepository _noticeRepository;

    public NoticeService(INoticeRepository noticeRepository)
    {
        _noticeRepository = noticeRepository;
    }

    // 클라이언트용 — 최신 활성 공지 1개 반환 (없으면 null)
    public async Task<NoticeDto?> GetLatestAsync()
    {
        var notice = await _noticeRepository.GetLatestActiveAsync();
        return notice is null ? null : new NoticeDto(notice.Id, notice.Content);
    }

    // Admin용 — 전체 공지 목록
    public async Task<List<NoticeAdminDto>> GetAllAsync()
    {
        var notices = await _noticeRepository.GetAllAsync();
        return notices.Select(n => new NoticeAdminDto(n.Id, n.Content, n.IsActive, n.CreatedAt, n.UpdatedAt)).ToList();
    }

    // Admin용 — 공지 생성
    public async Task<NoticeAdminDto> CreateAsync(CreateNoticeDto dto)
    {
        var notice = new Domain.Entities.Notice { Content = dto.Content };
        await _noticeRepository.AddAsync(notice);
        await _noticeRepository.SaveChangesAsync();
        return new NoticeAdminDto(notice.Id, notice.Content, notice.IsActive, notice.CreatedAt, notice.UpdatedAt);
    }

    // Admin용 — 공지 수정
    public async Task<bool> UpdateAsync(int id, UpdateNoticeDto dto)
    {
        var notice = await _noticeRepository.GetByIdAsync(id);
        if (notice is null) return false;
        notice.Content = dto.Content;
        notice.IsActive = dto.IsActive;
        notice.UpdatedAt = DateTime.UtcNow;
        await _noticeRepository.SaveChangesAsync();
        return true;
    }

    // Admin용 — 공지 삭제
    public async Task<bool> DeleteAsync(int id)
    {
        var notice = await _noticeRepository.GetByIdAsync(id);
        if (notice is null) return false;
        _noticeRepository.Delete(notice);
        await _noticeRepository.SaveChangesAsync();
        return true;
    }
}
