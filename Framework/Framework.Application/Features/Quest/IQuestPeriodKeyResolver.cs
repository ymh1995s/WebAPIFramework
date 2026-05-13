using Framework.Domain.Enums;

namespace Framework.Application.Features.Quest;

// 퀘스트 주기 키 계산 인터페이스 — KST 기준으로 주기에 맞는 문자열 키를 반환
public interface IQuestPeriodKeyResolver
{
    // 현재 시각 기준으로 주기에 맞는 PeriodKey를 반환한다.
    // Daily: "yyyy-MM-dd" (KST), Weekly: "yyyy-Www" (ISO 8601, KST), Permanent: "permanent"
    string Resolve(QuestPeriod period);
}
