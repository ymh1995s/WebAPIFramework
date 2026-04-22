namespace Framework.Application.DTOs;

// 랭킹 응답 DTO
public record RankingDto(int Rank, int PlayerId, string Nickname, int BestScore);
