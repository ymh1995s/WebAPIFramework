namespace Framework.Domain.Interfaces;

// 작업 단위 인터페이스 — 명시적 트랜잭션 경계를 정의
// 여러 Repository의 변경을 하나의 DB 트랜잭션으로 원자적으로 커밋/롤백할 때 사용
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// [경고] EnableRetryOnFailure 활성 환경에서는 본 단독 호출 시 ExecutionStrategy retry 미적용.
    /// 일반 트랜잭션은 ExecuteInTransactionAsync 사용 권장. 본 메서드는 향후 호환을 위해 유지하지만
    /// 새 코드에서는 사용 금지.
    /// </summary>
    Task BeginTransactionAsync();

    // 트랜잭션 커밋 — 미저장 변경사항 flush 후 DB에 확정
    Task CommitAsync();

    // 트랜잭션 롤백 — 트랜잭션 내 모든 변경사항을 취소
    Task RollbackAsync();

    // 트랜잭션 스코프 실행 — 활성 트랜잭션 없으면 소유자로 시작, 있으면 참여자로 합류
    // 소유자: 성공 시 SaveChanges + 커밋, 실패 시 롤백 / 참여자: 람다 실행만
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work);
    Task ExecuteInTransactionAsync(Func<Task> work);

    // UNIQUE 위반 catch 후 ChangeTracker 상태 정리 — 미제거 시 람다 종료 후 SaveChangesAsync에서 재시도됨
    void DetachEntry<T>(T entity) where T : class;

    // ChangeTracker 전체 정리 — 동시성 충돌 재시도 시 stale 엔티티 제거 용도
    // DbUpdateConcurrencyException catch 후 재시도 전에 호출하여 오염된 엔티티 상태를 초기화
    void ClearChangeTracker();
}
