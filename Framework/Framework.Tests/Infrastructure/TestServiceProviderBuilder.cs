using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Tests.Infrastructure;

// 테스트에서 ServiceCollection을 부분 빌드 + AppDbContext를 InMemory로 등록
// 운영 ServiceExtensions의 Add*Repositories() / Add*Services() 메서드와 함께 사용
public static class TestServiceProviderBuilder
{
    // 기본 서비스 컬렉션 생성 — 로깅 + InMemory AppDbContext 사전 등록
    // 운영의 UseNpgsql 대체용이므로, 이후에 Add*Repositories() / Add*Services() 호출로 필요한 서비스 추가
    public static IServiceCollection CreateBaseServices(string? databaseName = null)
    {
        var services = new ServiceCollection();

        // 로깅 — 테스트 시 콘솔 의존 회피
        services.AddLogging();

        // AppDbContext를 InMemory로 등록 (운영의 UseNpgsql 대체)
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString()));

        // IMemoryCache 등록 — LevelTableProvider 등 캐시 의존 서비스용 (운영 Program.cs AddMemoryCache()와 동일)
        services.AddMemoryCache();

        return services;
    }
}
