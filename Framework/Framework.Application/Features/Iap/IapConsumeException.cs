using Framework.Domain.Enums;

namespace Framework.Application.Features.Iap;

// IAP consume API 호출 실패 예외
// IsPermanent=true : 400/404/410 — 재시도해도 무의미, 즉시 중단
// IsPermanent=false: 500/503/네트워크 오류 — 일시적 오류, retry 워커가 재시도
public class IapConsumeException : Exception
{
    // 실패가 발생한 스토어 종류
    public IapStore Store { get; }

    // 영구실패 여부 — true면 재시도 중단, false면 retry 워커 위임
    public bool IsPermanent { get; }

    public IapConsumeException(IapStore store, string message, bool isPermanent, Exception? inner = null)
        : base(message, inner)
    {
        Store = store;
        IsPermanent = isPermanent;
    }
}
