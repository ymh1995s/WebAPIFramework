namespace Framework.Application.Features.AdReward;

// 광고 SSV 콜백 컨텍스트 — 네트워크별 검증기에 전달되는 원본 요청 정보
public record AdCallbackContext(
    // 쿼리 파라미터 전체 (네트워크별 파라미터 이름이 다르므로 Dictionary로 전달)
    IReadOnlyDictionary<string, string> QueryParams,

    // 원본 쿼리 스트링 (HMAC 서명 검증에 사용)
    string RawQueryString,

    // 요청 발신 IP (보안 감시 목적)
    string RemoteIp
);
