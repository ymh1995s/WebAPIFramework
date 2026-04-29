using Framework.Domain.Enums;

namespace Framework.Application.Features.AdReward;

// 광고 네트워크 검증기 팩토리 인터페이스 — Strategy 패턴
// AdNetworkType을 받아 적절한 IAdNetworkVerifier를 반환
public interface IAdNetworkVerifierResolver
{
    // 네트워크 타입에 맞는 검증기 반환
    // 등록되지 않은 네트워크이면 NotSupportedException 발생
    IAdNetworkVerifier Resolve(AdNetworkType network);
}
