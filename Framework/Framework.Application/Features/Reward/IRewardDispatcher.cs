namespace Framework.Application.Features.Reward;

// 보상 지급 단일 진입점 인터페이스
// 모든 보상 경로(게임 완료·퀘스트·레벨업·Admin 직접 지급 등)는 이 인터페이스를 통한다
public interface IRewardDispatcher
{
    // 보상 지급 — 멱등성 검사 → 번들 분기 → 지급 → AuditLog 기록
    Task<GrantRewardResult> GrantAsync(GrantRewardRequest request);
}
