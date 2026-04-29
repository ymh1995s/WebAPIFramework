using Framework.Application.Features.Iap;
using Framework.Domain.Enums;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Api.Services.IapStore;

// 스토어 영수증 검증기 팩토리 구현체 — Strategy 패턴
// DI 컨테이너에 등록된 모든 IIapStoreVerifier를 Dictionary로 매핑
public class IapStoreVerifierResolver : IIapStoreVerifierResolver
{
    // 스토어 타입 → 검증기 매핑 테이블
    private readonly Dictionary<IapStoreEnum, IIapStoreVerifier> _map;

    public IapStoreVerifierResolver(IEnumerable<IIapStoreVerifier> verifiers)
    {
        // 주입받은 모든 검증기를 Store 타입 기준으로 딕셔너리로 변환
        _map = verifiers.ToDictionary(v => v.Store);
    }

    // 스토어 타입에 맞는 검증기 반환 — 미등록 시 예외 발생
    public IIapStoreVerifier Resolve(IapStoreEnum store)
    {
        if (_map.TryGetValue(store, out var verifier))
            return verifier;

        throw new IapVerifierException(store, $"지원하지 않는 스토어입니다: {store}");
    }
}
