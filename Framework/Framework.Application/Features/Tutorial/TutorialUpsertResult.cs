namespace Framework.Application.Features.Tutorial;

// 튜토리얼 upsert 결과 상태 코드
public enum TutorialUpsertStatus
{
    Success,
    KeyTooLong,        // Key가 128자 초과
    ValueTooLong,      // Value가 512자 초과
    LimitExceeded      // 플레이어 행 200개 초과 (신규 삽입 시에만)
}

// 튜토리얼 upsert 처리 결과
public record TutorialUpsertResult(TutorialUpsertStatus Status);
