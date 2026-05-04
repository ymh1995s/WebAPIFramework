namespace Framework.Application.BackgroundServices;

// PII 보관기간 정책 — appsettings.json `PiiRetention` 섹션에서 바인딩
// 각 기간은 법규 기준값을 기본값으로 설정하여 미설정 시에도 안전하게 동작
public class PiiRetentionOptions
{
    // AuditLog 보관 일수 (안전성 확보조치 기준 §8 — 1년 이상)
    public int AuditLogDays { get; set; } = 365;

    // RateLimitLog 보관 일수 (통비법 시행령 §41 — 3개월 이상)
    public int RateLimitLogDays { get; set; } = 90;

    // BanLog ActorIp NULL 익명화 시점 (행은 영구 보존 — 운영 이력 참조 보장)
    public int BanLogActorIpDays { get; set; } = 365;

    // IapPurchase.ClientIp NULL 익명화 시점 (상태 종결 후 — 본문은 전자상거래법 §6에 따라 5년 보존)
    public int IapPurchaseClientIpDays { get; set; } = 90;

    // 매일 실행 시각 (KST) — 트래픽 최저 구간인 새벽 3시 기본값
    public int DailyRunHourKst { get; set; } = 3;

    // 1회 ExecuteDelete/ExecuteUpdate 청크 크기 — 락 시간과 메모리 사용량 균형점
    public int BatchSize { get; set; } = 5000;

    // 비상 정지 스위치 — false 시 Cleanup 전체 건너뜀 (인시던트 대응 시 즉시 중단 가능)
    public bool Enabled { get; set; } = true;
}
