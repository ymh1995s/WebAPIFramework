using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 1회 공지 리포지토리 인터페이스
public interface IShoutRepository
{
    // 플레이어 접속 시 활성 1회 공지 조회 — 전체 대상(PlayerId=null) + 해당 플레이어 대상 포함
    Task<List<Shout>> GetActiveForPlayerAsync(int playerId);

    // 1회 공지 추가
    Task<Shout> AddAsync(Shout shout);

    // Admin 이력 조회 — 필터 조합, 페이지네이션
    Task<(List<Shout> items, int total)> SearchAsync(int? playerId, bool? activeOnly, int page, int pageSize);

    // ID로 단건 조회
    Task<Shout?> GetByIdAsync(int id);

    // 변경 사항 저장
    Task SaveChangesAsync();
}
