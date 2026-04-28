namespace Framework.Application.Features.Ranking;

// 랭킹 응답 DTO — PlayerId는 외부 공개용 Guid (내부 정수 Id 노출 금지)
public record RankingDto(int Rank, Guid PlayerId, string Nickname, int BestScore);
