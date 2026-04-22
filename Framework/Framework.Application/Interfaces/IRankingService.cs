using Framework.Application.DTOs;

// 랭킹 서비스 인터페이스
public interface IRankingService
{
    // 상위 N명 랭킹 조회
    Task<List<RankingDto>> GetTopRankingsAsync(int count = 100);

    // 내 순위 조회
    Task<RankingDto> GetMyRankingAsync(int playerId);
}
