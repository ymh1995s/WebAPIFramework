namespace Framework.Application.BackgroundServices;

// PII 정리 서비스의 최근 실행 결과를 메모리에 보관하는 공유 상태 객체
// Singleton으로 등록되어 PiiRetentionCleanupService(작성)와 PiiRetentionHealthCheck(읽기)가 공유
// thread-safe: Interlocked / volatile 없이 읽기 전용 필드 교환으로 구현 (업데이트 빈도 낮음)
public sealed class PiiRetentionHealthState
{
    // 마지막 성공 실행 시각 (UTC) — null이면 아직 한 번도 성공하지 않음
    public DateTime? LastSuccessUtc { get; private set; }

    // 마지막 실패 실행 시각 (UTC) — null이면 실패 이력 없음
    public DateTime? LastFailureUtc { get; private set; }

    // 마지막 실패 시 발생한 예외 메시지 — HealthCheck 응답 description 필드에 노출
    public string? LastFailureMessage { get; private set; }

    // 정리 작업 성공 시 호출 — LastSuccessUtc 갱신
    public void MarkSuccess(DateTime utcNow)
    {
        LastSuccessUtc = utcNow;
    }

    // 정리 작업 실패 시 호출 — LastFailureUtc 및 예외 메시지 갱신
    public void MarkFailure(DateTime utcNow, Exception ex)
    {
        LastFailureUtc = utcNow;
        // 스택 트레이스 제외 — 민감 정보 노출 방지, 메시지만 보존
        LastFailureMessage = ex.Message;
    }
}
