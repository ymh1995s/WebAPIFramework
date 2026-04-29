namespace Framework.Domain.Interfaces;

// 작업 단위 인터페이스 — 명시적 트랜잭션 경계를 정의
// 여러 Repository의 변경을 하나의 DB 트랜잭션으로 원자적으로 커밋/롤백할 때 사용
public interface IUnitOfWork : IAsyncDisposable
{
    // 트랜잭션 시작
    Task BeginTransactionAsync();

    // 트랜잭션 커밋 — 미저장 변경사항 flush 후 DB에 확정
    Task CommitAsync();

    // 트랜잭션 롤백 — 트랜잭션 내 모든 변경사항을 취소
    Task RollbackAsync();
}
