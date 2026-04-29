using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 게임 결과 참가자 — 매치별 플레이어 기록 (GameMatchParticipant에서 이름 변경)
// UNIQUE(MatchId, PlayerId)로 한 매치에 동일 플레이어 중복 참가 방지
public class GameResultParticipant
{
    // 기본 키
    public int Id { get; set; }

    // 매치 FK (Guid)
    public Guid MatchId { get; set; }

    // 플레이어 FK
    public int PlayerId { get; set; }

    // 참가자 유형 (실제 플레이어 또는 봇)
    public HumanType HumanType { get; set; } = HumanType.Human;

    // 획득 점수 (게임 종료 전이면 null)
    public int? Score { get; set; }

    // 개인 결과 (게임 종료 전이면 null)
    public MatchOutcome? Result { get; set; }

    // 매치 네비게이션 프로퍼티
    public GameResult Match { get; set; } = null!;

    // 플레이어 네비게이션 프로퍼티
    public Player Player { get; set; } = null!;
}
