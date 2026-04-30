// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

namespace Framework.Domain.Content.Entities;

// 플레이어 스테이지 클리어 기록 엔티티
// UNIQUE(PlayerId, StageId) — 동일 스테이지 중복 행 방지, upsert 패턴 사용
public class StageClear
{
    // 기본 키
    public int Id { get; set; }

    // 클리어한 플레이어 ID (FK → Players.Id)
    public int PlayerId { get; set; }

    // 클리어한 스테이지 ID (FK → Stages.Id)
    public int StageId { get; set; }

    // 최초 클리어 일시 (UTC)
    public DateTime FirstClearedAt { get; set; }

    // 마지막 클리어 일시 (UTC)
    public DateTime LastClearedAt { get; set; }

    // 누적 클리어 횟수 (최초 = 1)
    public int ClearCount { get; set; } = 1;

    // 최고 점수
    public int BestScore { get; set; } = 0;

    // 획득한 최고 별 수 (0~3)
    public int BestStars { get; set; } = 0;

    // 최단 클리어 시간 (밀리초) — 0이면 미측정
    public int BestClearTimeMs { get; set; } = 0;
}
