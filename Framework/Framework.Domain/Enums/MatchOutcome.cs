namespace Framework.Domain.Enums;

// 매치 참가자 개인 결과
public enum MatchOutcome
{
    // 승리
    Win,

    // 패배
    Lose,

    // 무승부
    Draw,

    // 이탈 (중도 포기 또는 연결 끊김)
    Abandon,
}
