using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// 스토어 종류에 따른 검증기 해석자 인터페이스 — DI 컨테이너에서 적절한 IIapStoreVerifier 반환
public interface IIapStoreVerifierResolver
{
    // 스토어 종류에 맞는 검증기 반환 — 등록되지 않은 스토어일 경우 예외 발생
    IIapStoreVerifier Resolve(IapStore store);
}
