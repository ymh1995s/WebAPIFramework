using Framework.Domain.Common.Exceptions;

namespace Framework.Application.Features.AdReward;

// 광고 콜백 서명 검증 실패 — 위변조 또는 잘못된 시크릿 키
public class InvalidAdSignatureException : DomainException
{
    public override string ErrorCode => "AD_INVALID_SIGNATURE";

    public InvalidAdSignatureException(string network, string reason)
        : base($"[{network}] 광고 콜백 서명 검증 실패: {reason}") { }
}

// 광고 정책을 찾을 수 없음 — 등록되지 않은 PlacementId
public class AdPolicyNotFoundException : DomainException
{
    public override string ErrorCode => "AD_POLICY_NOT_FOUND";

    public AdPolicyNotFoundException(string network, string placementId)
        : base($"[{network}] 광고 정책을 찾을 수 없습니다. PlacementId: {placementId}") { }
}

// 하루 광고 보상 한도 초과
public class AdDailyLimitExceededException : DomainException
{
    public override string ErrorCode => "AD_DAILY_LIMIT_EXCEEDED";

    public AdDailyLimitExceededException(int playerId, string placementId, int limit)
        : base($"일일 광고 보상 한도 초과 — PlayerId: {playerId}, PlacementId: {placementId}, 한도: {limit}회") { }
}
