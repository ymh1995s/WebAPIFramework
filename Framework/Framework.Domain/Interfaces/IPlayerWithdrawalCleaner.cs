namespace Framework.Domain.Interfaces;

// 탈퇴 시 게임 진행 데이터 정리 서비스 인터페이스
// 재가입 시 기존 데이터 복구 차단 목적으로 즉시 hard delete 수행
public interface IPlayerWithdrawalCleaner
{
    // 게임 진행 데이터 hard delete — 아래 테이블 대상 (PlayerId 기준)
    // 대상: PlayerProfile / PlayerItem / Mail / MailItem / DailyLoginLog /
    //       GameResultParticipant / RewardGrant / Inquiry / Shout / StageClear
    // 비대상(보존): IapPurchase (전자상거래법 5년 보관 의무) / AuditLog / BanLog (운영 추적)
    Task PurgeGameDataAsync(int playerId);
}
