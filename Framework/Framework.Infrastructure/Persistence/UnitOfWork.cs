using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Framework.Infrastructure.Persistence;

// IUnitOfWork 구현체 — AppDbContext의 DB 트랜잭션을 래핑
// 여러 Repository 변경을 단일 트랜잭션으로 묶어 원자성을 보장
// IAsyncDisposable로 미완료 트랜잭션 자동 정리
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    // 현재 활성 트랜잭션 — null이면 트랜잭션 미시작 상태
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // 트랜잭션 시작 — BeginTransactionAsync 호출 후 Commit/Rollback 중 하나 반드시 호출
    public async Task BeginTransactionAsync()
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync();
    }

    // 트랜잭션 커밋 — EF Change Tracker의 미저장 변경 flush 후 DB 확정
    // SaveChangesAsync → CommitAsync 순서로 호출하여 추적 중인 모든 변경이 누락 없이 반영됨
    public async Task CommitAsync()
    {
        if (_transaction is null)
            throw new InvalidOperationException("활성 트랜잭션이 없습니다. BeginTransactionAsync를 먼저 호출하세요.");

        // Change Tracker에 남아있는 미저장 변경사항을 DB로 flush
        await _dbContext.SaveChangesAsync();
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    // 트랜잭션 롤백 — 트랜잭션 내 모든 변경사항 취소 후 트랜잭션 해제
    public async Task RollbackAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    // 트랜잭션 스코프 실행 (반환값 있음) — 활성 트랜잭션 없으면 소유자로 시작, 있으면 참여자로 합류
    // 소유자: 성공 시 SaveChangesAsync + CommitAsync, 실패 시 RollbackAsync
    // 참여자: 람다 실행만 (커밋/롤백은 소유자에게 위임)
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work)
    {
        // 현재 활성 트랜잭션이 없으면 소유자, 있으면 참여자
        var isOwner = _transaction is null;

        if (isOwner)
            _transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var result = await work();

            if (isOwner)
            {
                // 소유자만 최종 flush 후 커밋
                await _dbContext.SaveChangesAsync();
                await _transaction!.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }

            return result;
        }
        catch
        {
            if (isOwner)
                await RollbackAsync();
            throw;
        }
    }

    // 트랜잭션 스코프 실행 (반환값 없음) — 제네릭 버전으로 위임
    public async Task ExecuteInTransactionAsync(Func<Task> work)
    {
        // 반환값 없는 버전 — 제네릭 버전으로 위임
        await ExecuteInTransactionAsync(async () => { await work(); return true; });
    }

    // ChangeTracker에서 엔티티 분리 — Detached 상태로 전환하면 이후 SaveChangesAsync에서 무시됨
    // UNIQUE 위반 catch 후 실패 엔티티를 ChangeTracker에서 제거할 때 사용
    public void DetachEntry<T>(T entity) where T : class
    {
        // ChangeTracker에서 엔티티 분리 — Detached 상태로 전환하면 이후 SaveChangesAsync에서 무시됨
        _dbContext.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
    }

    // ChangeTracker 전체 초기화 — 동시성 충돌(DbUpdateConcurrencyException) 재시도 전 호출
    // 충돌 후 stale 상태로 남은 모든 엔티티를 추적 해제하여 다음 시도에서 DB 최신값으로 재조회
    public void ClearChangeTracker() => _dbContext.ChangeTracker.Clear();

    // 미완료 트랜잭션 자동 정리 — DI 스코프 종료 시 안전망
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
