using Framework.Domain.ValueObjects;

namespace Framework.Application.Features.Reward;

// 보상 계산기 제네릭 인터페이스 — 특정 컨텍스트를 받아 RewardBundle을 결정
// TContext: 보상 계산에 필요한 컨텍스트 타입 (매치 결과, 퀘스트 완료 정보 등)
public interface IRewardCalculator<in TContext>
{
    // 컨텍스트를 분석하여 지급할 보상 번들 반환
    // 보상이 없으면 RewardBundle.IsEmpty == true인 번들 반환
    Task<RewardBundle> CalculateAsync(TContext context);
}
