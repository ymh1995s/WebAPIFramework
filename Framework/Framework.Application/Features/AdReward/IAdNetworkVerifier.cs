using Framework.Domain.Enums;

namespace Framework.Application.Features.AdReward;

// 광고 네트워크 서버 검증(SSV) 전략 인터페이스
// 각 광고 네트워크(UnityAds, IronSource 등)가 이 인터페이스를 구현
public interface IAdNetworkVerifier
{
    // 이 검증기가 담당하는 광고 네트워크 타입
    AdNetworkType Network { get; }

    // 콜백 서명 검증 및 파라미터 파싱
    // 검증 실패 시 InvalidAdSignatureException 발생
    Task<AdCallbackVerified> VerifyAsync(AdCallbackContext context);
}
