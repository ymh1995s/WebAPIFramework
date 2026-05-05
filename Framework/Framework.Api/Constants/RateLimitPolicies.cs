namespace Framework.Api.Constants;

// Rate Limit 정책 이름 상수 — ServiceExtensions 등록 + Controller 부착 양측 참조
// 매직 문자열 산재 시 오타로 silent fail(정책 미적용 무한 호출) 위험 차단 (M-23)
public static class RateLimitPolicies
{
    // 인증 엔드포인트 — 미인증 IP / 인증 PlayerId 분기
    public const string Auth = "auth";

    // 인게임 공통 API — PlayerId 파티션
    public const string Game = "game";

    // IAP 결제 검증
    public const string IapVerify = "iap-verify";

    // Google Pub/Sub RTDN 수신
    public const string IapRtdn = "iap-rtdn";

    // 광고 SSV 콜백 — 광고 네트워크 서버 IP 기준
    public const string AdsCallback = "ads-callback";
}
