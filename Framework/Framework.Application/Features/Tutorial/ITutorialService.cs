namespace Framework.Application.Features.Tutorial;

// 튜토리얼 서비스 인터페이스
public interface ITutorialService
{
    // 플레이어의 전체 튜토리얼 진행 상태 조회
    Task<Dictionary<string, string>> GetProgressAsync(int playerId);

    // 튜토리얼 진행 상태 키-값 upsert
    Task<TutorialUpsertResult> UpsertProgressAsync(int playerId, string key, string value);
}
