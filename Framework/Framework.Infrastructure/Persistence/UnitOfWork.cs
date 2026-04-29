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
