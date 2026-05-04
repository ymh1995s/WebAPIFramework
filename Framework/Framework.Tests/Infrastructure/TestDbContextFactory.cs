using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Tests.Infrastructure;

// 테스트용 InMemory AppDbContext 빠르게 생성
// 주의: PostgreSQL 특화 기능(xmin 동시성 토큰, Raw SQL 등)은 InMemory에서 동작하지 않음
public static class TestDbContextFactory
{
    // InMemory DB 인스턴스 생성
    // databaseName을 지정하면 같은 이름끼리 상태 공유 — 테스트 격리 목적이면 null 권장(GUID 자동 생성)
    public static AppDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
