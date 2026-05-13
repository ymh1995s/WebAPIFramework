using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 퀘스트 정의 저장소 인터페이스
public interface IQuestDefinitionRepository
{
    // ID로 단건 조회 (null 가능)
    Task<QuestDefinition?> GetByIdAsync(int id);

    // 활성 퀘스트 정의 전체 조회 — IncrementAsync 캐시 갱신용
    Task<List<QuestDefinition>> GetActiveListAsync();

    // 주기별 활성 퀘스트 조회 — 클라이언트 목록 표시용
    Task<List<QuestDefinition>> GetActiveByPeriodAsync(QuestPeriod period);

    // Admin 검색 — 키워드 + 주기 필터 + 페이지네이션
    Task<(List<QuestDefinition> Items, int TotalCount)> SearchAsync(
        string? keyword, QuestPeriod? period, bool? isActive, int page, int pageSize);

    // 퀘스트 정의 추가
    Task AddAsync(QuestDefinition quest);

    // 변경사항 저장
    Task SaveChangesAsync();
}
