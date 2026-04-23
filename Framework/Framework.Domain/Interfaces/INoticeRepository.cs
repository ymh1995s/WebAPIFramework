using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 공지 저장소 인터페이스
public interface INoticeRepository
{
    // 클라이언트용 — 가장 최근 활성 공지 1개
    Task<Notice?> GetLatestActiveAsync();
    // Admin용 — 전체 공지 조회
    Task<List<Notice>> GetAllAsync();
    // ID로 단건 조회
    Task<Notice?> GetByIdAsync(int id);
    Task AddAsync(Notice notice);
    void Delete(Notice notice);
    Task SaveChangesAsync();
}
