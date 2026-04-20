namespace Framework.Application.DTOs;

// 플레이어 기록 응답 DTO
public record PlayerRecordDto(int Id, string Nickname, float PlayTime, int Score, DateTime CreatedAt);

// 플레이어 기록 생성 요청 DTO
public record CreatePlayerRecordDto(string Nickname, float PlayTime, int Score);
