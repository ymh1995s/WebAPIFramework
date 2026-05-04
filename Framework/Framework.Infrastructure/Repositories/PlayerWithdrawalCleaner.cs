using Framework.Domain.Content.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 탈퇴 플레이어의 게임 진행 데이터 정리 구현체
// AppDbContext를 주입받아 ExecuteDeleteAsync로 각 테이블을 직접 삭제 (변경 추적 없이 DELETE SQL 직행)
// SaveChanges는 호출자(AuthService.WithdrawAsync) 트랜잭션에서 담당
public class PlayerWithdrawalCleaner : IPlayerWithdrawalCleaner
{
    private readonly AppDbContext _db;

    public PlayerWithdrawalCleaner(AppDbContext db)
    {
        _db = db;
    }

    // 게임 진행 데이터 일괄 hard delete
    // 순서: MailItem(선) → Mail → 나머지 순으로 FK 의존성 고려
    // IapPurchase / AuditLog / BanLog는 삭제하지 않음 (보존 의무 + 운영 추적)
    public async Task PurgeGameDataAsync(int playerId)
    {
        // MailItem 먼저 삭제 — Mail FK 제약으로 Mail 삭제 전 반드시 선행
        // (DB에 CASCADE가 없는 경우 대비한 명시적 처리)
        await _db.MailItems
            .Where(mi => mi.Mail.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 우편 삭제 — 미수령 포함 전체 제거
        await _db.Mails
            .Where(m => m.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 인벤토리(보유 아이템 목록) 삭제
        await _db.PlayerItems
            .Where(pi => pi.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 인게임 프로필 삭제 — 레벨, 경험치, 재화
        await _db.PlayerProfiles
            .Where(pp => pp.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 일일 로그인 기록 삭제
        await _db.DailyLoginLogs
            .Where(l => l.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 게임 결과 참가 기록 삭제 — 점수/랭킹 데이터 포함
        await _db.GameResultParticipants
            .Where(p => p.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 보상 지급 이력 삭제 — 재가입 시 중복 지급 방지 멱등성 키도 함께 제거됨
        await _db.RewardGrants
            .Where(g => g.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 소원수리함 문의 삭제 — 미답변 문의 포함
        await _db.Inquiries
            .Where(i => i.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 1회 공지 삭제 — 특정 플레이어 대상 공지 (PlayerId nullable이므로 null 체크 포함)
        await _db.Shouts
            .Where(s => s.PlayerId != null && s.PlayerId == playerId)
            .ExecuteDeleteAsync();

        // 스테이지 클리어 기록 삭제 — 재가입 시 처음부터 시작하도록 전체 제거
        await _db.StageClears
            .Where(c => c.PlayerId == playerId)
            .ExecuteDeleteAsync();
    }
}
