using Framework.Domain.Enums;

namespace Framework.Application.Features.AdReward;

// 광고 보상 처리 서비스 인터페이스
// 검증 → 정책 조회 → 일일 한도 체크 → 보상 지급 파이프라인 진입점
public interface IAdRewardService
{
    // 광고 SSV 콜백 처리 — 검증부터 보상 지급까지 전체 파이프라인 실행
    Task<AdRewardResult> ProcessCallbackAsync(AdNetworkType network, AdCallbackContext ctx);
}
