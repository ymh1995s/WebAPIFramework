using Framework.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Framework.Tests.Unit.Smoke;

// 빌드/DI 검증용 스모크 테스트 — InMemory AppDbContext 생성 + 기본 쿼리 동작 확인
public class DbContextSmokeTests
{
    [Fact]
    public async Task CreateAndQuery_DoesNotThrow()
    {
        // 인프라 헬퍼로 InMemory DbContext 생성
        await using var db = TestDbContextFactory.Create();

        // 모델 매핑이 InMemory에서 정상 작동하는지 — Players 컬렉션 단순 쿼리
        // xUnit v3: TestContext.Current.CancellationToken으로 테스트 취소 신호 전달
        var players = await db.Players.ToListAsync(TestContext.Current.CancellationToken);

        // 빈 DB라 0건 반환되어야 함
        Assert.Empty(players);
    }
}
