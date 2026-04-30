using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 게임 결과 저장소 인터페이스 (IGameMatchRepository에서 이름 변경)
public interface IGameResultRepository
{
    // ID로 결과 단건 조회 (참가자 포함)
    Task<GameResult?> GetByIdAsync(Guid matchId);

    // ID로 결과 단건 조회 (참가자 + 플레이어 프로필 포함)
    Task<GameResult?> GetByIdWithParticipantsAsync(Guid matchId);

    // 새 게임 결과 추가
    Task AddAsync(GameResult match);

    // 플레이어별 최고 점수 기준 상위 N명 조회 (랭킹용)
    Task<List<(int PlayerId, Guid PublicId, string Nickname, int BestScore)>> GetTopRankingsAsync(int count);

    // 특정 플레이어의 최고 점수 조회
    Task<int> GetPlayerBestScoreAsync(int playerId);

    // 특정 플레이어의 순위 조회
    Task<int> GetPlayerRankAsync(int playerId);

    // Admin 필터 검색 — 결과 목록 페이지네이션 (Participants 전체 로딩 없이 COUNT 서브쿼리 포함)
    Task<(List<(GameResult Match, int ParticipantCount)> Items, int TotalCount)> SearchAsync(
        Guid? matchId, int? playerId, Tier? tier, MatchState? state,
        DateTime? from, DateTime? to, int page, int pageSize);

    // 변경사항 저장
    Task SaveChangesAsync();
}
