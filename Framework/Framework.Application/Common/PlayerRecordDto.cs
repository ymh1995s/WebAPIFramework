namespace Framework.Application.Common;

// 플레이어 기록 응답 DTO
public record PlayerRecordDto(int Id, int PlayerId, float PlayTime, int Score, DateTime CreatedAt);

// 플레이어 기록 생성 요청 DTO
public record CreatePlayerRecordDto(int PlayerId, float PlayTime, int Score);
