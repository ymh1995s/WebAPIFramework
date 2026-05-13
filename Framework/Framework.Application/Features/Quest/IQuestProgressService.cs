using Framework.Domain.Enums;

namespace Framework.Application.Features.Quest;

// 플레이어 퀘스트 진행 서비스 인터페이스
public interface IQuestProgressService
{
    // 퀘스트 조건 카운터 증가 — 기존 서비스(StageClear/ItemUse/ShopPurchase/DailyLogin)에서 호출
    // conditionType: 조건 타입, amount: 증가량, targetId: 조건 대상 ID (null이면 타입 전체)
    Task IncrementAsync(int playerId, QuestConditionType conditionType, int amount, int? targetId);

    // 플레이어의 특정 주기 퀘스트 목록 조회 — 클라이언트 퀘스트 화면 표시용
    Task<List<QuestProgressDto>> GetProgressListAsync(int playerId, QuestPeriod period);

    // 퀘스트 보상 수령 처리 — IsClaimed 전환 + 보상 지급
    // 반환값: ("success"|"alreadyClaimed"|"notEligible"|"notFound")
    Task<string> ClaimAsync(int playerId, int questId);
}
