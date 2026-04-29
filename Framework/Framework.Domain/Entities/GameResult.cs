using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 게임 결과 엔티티 — 매치 단위 영속화 (GameMatch에서 이름 변경)
public class GameResult
{
    // 기본 키 — Guid 사용 (분산 환경에서도 충돌 없는 식별자)
    public Guid Id { get; set; } = Guid.NewGuid();

    // 매치 티어 (Bronze, Silver, Gold 등)
    public Tier Tier { get; set; }

    // 매치 시작 일시 (UTC)
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // 매치 종료 일시 (UTC) — 진행 중이면 null
    public DateTime? EndedAt { get; set; }

    // 현재 매치 상태
    public MatchState State { get; set; } = MatchState.Waiting;

    // 참가자 목록 네비게이션 프로퍼티
    public ICollection<GameResultParticipant> Participants { get; set; } = new List<GameResultParticipant>();
}
