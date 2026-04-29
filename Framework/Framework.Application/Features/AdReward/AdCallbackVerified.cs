namespace Framework.Application.Features.AdReward;

// 광고 SSV 콜백 검증 성공 결과 — 검증기(IAdNetworkVerifier)가 반환하는 파싱된 데이터
public record AdCallbackVerified(
    // 보상을 받을 플레이어 ID (콜백 파라미터에서 추출)
    int PlayerId,

    // 광고 네트워크에서 관리하는 게재 위치 식별자
    string PlacementId,

    // 광고 네트워크가 발급하는 트랜잭션 고유 ID (멱등성 키에 사용)
    string TransactionId,

    // 광고 시청 완료 시각 (UTC)
    DateTime EventTime
);
