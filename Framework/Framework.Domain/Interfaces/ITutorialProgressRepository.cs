using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 튜토리얼 진행 상태 저장소 인터페이스
public interface ITutorialProgressRepository
{
    // 플레이어의 전체 튜토리얼 진행 키-값 맵 조회
    Task<Dictionary<string, string>> GetAllByPlayerIdAsync(int playerId);

    // 행 수 카운트 — 상한 검사용
    Task<int> CountByPlayerIdAsync(int playerId);

    // 특정 키 조회 (upsert 판단용)
    Task<TutorialProgress?> GetByPlayerAndKeyAsync(int playerId, string key);

    // 신규 행 추가
    Task AddAsync(TutorialProgress progress);

    // 변경사항 저장
    Task SaveChangesAsync();
}
