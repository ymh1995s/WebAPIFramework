using Framework.Application.DTOs;

namespace Framework.Application.Interfaces;

// 공지 서비스 인터페이스
public interface INoticeService
{
    // 클라이언트용 — 가장 최근 활성 공지 1개 (없으면 null)
    Task<NoticeDto?> GetLatestAsync();
    // Admin용
    Task<List<NoticeAdminDto>> GetAllAsync();
    Task<NoticeAdminDto> CreateAsync(CreateNoticeDto dto);
    Task<bool> UpdateAsync(int id, UpdateNoticeDto dto);
    Task<bool> DeleteAsync(int id);
}
