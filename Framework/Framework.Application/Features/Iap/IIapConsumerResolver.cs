using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 스토어 종류에 맞는 IIapConsumer 반환 인터페이스
// IIapStoreVerifierResolver와 동일한 Resolver 패턴 적용
public interface IIapConsumerResolver
{
    // 스토어 종류에 맞는 consume 구현체 반환 — 등록되지 않은 스토어이면 예외
    IIapConsumer Resolve(IapStore store);
}
