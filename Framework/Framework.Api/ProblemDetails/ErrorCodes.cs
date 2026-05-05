namespace Framework.Api.ProblemDetails;

// API 에러 응답 errorCode 카탈로그 (M-13 P2)
// Unity 클라이언트가 이 상수를 기준으로 에러 분기 처리를 수행할 수 있다.
// 신규 도메인 예외 추가 시 본 클래스에 상수를 추가하고 해당 핸들러에서 참조한다.
public static class ErrorCodes
{
    // 일반 서버 내부 오류
    public const string InternalError = "INTERNAL_ERROR";

    // 입력 검증 오류
    public const string InvalidEnumValue = "INVALID_ENUM_VALUE";
    public const string ValidationFailed = "VALIDATION_FAILED";

    // 광고 관련 오류
    public const string AdSignatureInvalid = "AD_SIGNATURE_INVALID";
    public const string AdPolicyNotFound = "AD_POLICY_NOT_FOUND";
    public const string AdDailyLimitExceeded = "AD_DAILY_LIMIT_EXCEEDED";

    // 인앱결제(IAP) 관련 오류
    public const string IapProductNotFound = "IAP_PRODUCT_NOT_FOUND";
    public const string IapReceiptInvalid = "IAP_RECEIPT_INVALID";
    public const string IapTokenOwnershipMismatch = "IAP_TOKEN_OWNERSHIP_MISMATCH";
    public const string IapVerifierError = "IAP_VERIFIER_ERROR";

    // IAP verify 동시성 충돌 한도 초과 — 503 응답, 클라이언트 재시도 권고
    public const string IapVerifyConcurrencyExhausted = "IAP_VERIFY_CONCURRENCY_EXHAUSTED";
}
