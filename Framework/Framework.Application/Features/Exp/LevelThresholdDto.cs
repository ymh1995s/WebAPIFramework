namespace Framework.Application.Features.Exp;

// 레벨 임계값 DTO — API 요청/응답에 사용되는 전송 객체
public record LevelThresholdDto(int Level, int RequiredExp);

// 레벨 테이블 일괄 교체 요청 본문
public record ReplaceAllLevelThresholdsRequest(List<LevelThresholdDto> Items);
