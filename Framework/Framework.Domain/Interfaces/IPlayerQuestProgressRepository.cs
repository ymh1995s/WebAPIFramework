using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 플레이어 퀘스트 진행 상태 저장소 인터페이스
public interface IPlayerQuestProgressRepository
{
    // PlayerId + QuestId + PeriodKey 조합으로 단건 조회 (null 가능)
    Task<PlayerQuestProgress?> FindAsync(int playerId, int questId, string periodKey);

    // 플레이어의 특정 주기 퀘스트 진행 상태 전체 조회
    Task<List<PlayerQuestProgress>> GetByPlayerAndPeriodKeyAsync(int playerId, string periodKey);

    // 플레이어의 특정 퀘스트 진행 상태 전체 조회 (여러 PeriodKey)
    Task<List<PlayerQuestProgress>> GetByPlayerAndQuestAsync(int playerId, int questId);

    // 진행 상태 추가
    Task AddAsync(PlayerQuestProgress progress);

    // 변경사항 저장
    Task SaveChangesAsync();
}
