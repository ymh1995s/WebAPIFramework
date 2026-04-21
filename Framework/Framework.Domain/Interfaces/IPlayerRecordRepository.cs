using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 플레이어 기록 저장소 인터페이스
public interface IPlayerRecordRepository
{
    // 전체 목록 조회
    Task<List<PlayerRecord>> GetAllAsync();
    // 페이지 단위 조회
    Task<List<PlayerRecord>> GetPagedAsync(int page, int pageSize);
    // 전체 개수 조회
    Task<int> GetCountAsync();
    // ID로 단건 조회
    Task<PlayerRecord?> GetByIdAsync(int id);
    // 새 기록 추가
    Task AddAsync(PlayerRecord record);
    // 변경사항 저장
    Task SaveChangesAsync();
}
