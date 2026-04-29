using Framework.Application.Features.AdReward;
using Framework.Domain.Enums;

namespace Framework.Api.Services.AdNetwork;

// 광고 네트워크 검증기 팩토리 구현체 — Strategy 패턴
// DI 컨테이너에 등록된 모든 IAdNetworkVerifier를 Dictionary로 매핑
public class AdNetworkVerifierResolver : IAdNetworkVerifierResolver
{
    // 네트워크 타입 → 검증기 매핑 테이블
    private readonly Dictionary<AdNetworkType, IAdNetworkVerifier> _verifiers;

    public AdNetworkVerifierResolver(IEnumerable<IAdNetworkVerifier> verifiers)
    {
        // 주입받은 모든 검증기를 Network 타입 기준으로 딕셔너리로 변환
        _verifiers = verifiers.ToDictionary(v => v.Network);
    }

    // 네트워크 타입에 맞는 검증기 반환 — 미등록 시 예외 발생
    public IAdNetworkVerifier Resolve(AdNetworkType network)
    {
        if (_verifiers.TryGetValue(network, out var verifier))
            return verifier;

        throw new NotSupportedException($"지원하지 않는 광고 네트워크입니다: {network}");
    }
}
