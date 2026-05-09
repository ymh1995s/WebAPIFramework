namespace Framework.Domain.Entities;

// 튜토리얼 진행 상태 — 플레이어별 key-value 저장소
// 서버는 값 저장/반환만 담당, 시나리오 해석은 클라이언트 전담
public class TutorialProgress
{
    public int Id { get; set; }
    public int PlayerId { get; set; }

    // 튜토리얼 진행 상태 식별자 (최대 128자)
    public string Key { get; set; } = string.Empty;

    // 해당 키에 연결된 값 (최대 512자)
    public string Value { get; set; } = string.Empty;

    // 마지막 갱신 시각 (UTC)
    public DateTime UpdatedAt { get; set; }

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
