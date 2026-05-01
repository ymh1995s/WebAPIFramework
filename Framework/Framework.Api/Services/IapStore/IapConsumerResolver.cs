using Framework.Application.Features.Iap;
using IapStoreEnum = Framework.Domain.Enums.IapStore;

namespace Framework.Api.Services.IapStore;

// DI 기반 스토어별 IIapConsumer 해석자 — IapStoreVerifierResolver와 동일 패턴
// DI 컨테이너에 등록된 모든 IIapConsumer 구현체를 IEnumerable로 주입받아 스토어별 분기
public class IapConsumerResolver : IIapConsumerResolver
{
    // DI로 주입된 모든 IIapConsumer 구현체 목록
    private readonly IEnumerable<IIapConsumer> _consumers;

    public IapConsumerResolver(IEnumerable<IIapConsumer> consumers)
    {
        _consumers = consumers;
    }

    // 스토어 종류에 맞는 consume 구현체 반환 — 등록되지 않은 스토어이면 예외 발생
    public IIapConsumer Resolve(IapStoreEnum store)
        => _consumers.FirstOrDefault(c => c.Store == store)
           ?? throw new InvalidOperationException($"등록된 consume 구현체가 없습니다: {store}");
}
