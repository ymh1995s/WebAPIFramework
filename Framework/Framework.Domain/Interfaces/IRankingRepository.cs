using Framework.Domain.Entities;

// 랭킹 저장소 인터페이스
public interface IRankingRepository
{
    // 플레이어별 최고 점수 기준 상위 N명 조회 — PublicId 포함 (외부 노출용)
    Task<List<(int PlayerId, Guid PublicId, string Nickname, int BestScore)>> GetTopRankingsAsync(int count);

    // 특정 플레이어의 순위 조회
    Task<int> GetPlayerRankAsync(int playerId);

    // 특정 플레이어의 최고 점수 조회
    Task<int> GetPlayerBestScoreAsync(int playerId);
}
