using Framework.Api.Extensions;
using Framework.Application.Features.Exp;
using Framework.Application.Features.Reward;
using Framework.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Tests.Unit.Smoke;

// 빌드/DI 검증용 스모크 테스트 — ServiceExtensions Add*Services() 호출 후 핵심 의존성 Resolve 가능 확인
// 향후 Service 추가/이동 시 DI 등록 누락을 자동 탐지
public class DiSmokeTests
{
    [Fact]
    public async Task RewardDispatcher_CanBeResolved()
    {
        // 부분 DI 컨테이너 + InMemory DbContext
        var services = TestServiceProviderBuilder.CreateBaseServices();

        // 운영 저장소 등록
        // - AddAuthRepositories: IPlayerRepository, IPlayerProfileRepository (RewardDispatcher 직접 의존)
        // - AddGameRepositories: IRewardGrantRepository, IPlayerItemRepository, IMailRepository,
        //                        IAuditLogRepository, IItemRepository (AuditLogService 의존),
        //                        IRewardTableRepository (RewardDispatcher.GrantLevelUpRewardsAsync 의존) 등
        services.AddAuthRepositories();
        services.AddGameRepositories();

        // 운영 서비스 등록
        // - AddGameServices: IRewardDispatcher(RewardDispatcher), IUnitOfWork, IAuditLogService 등
        // - AddContentServices: IExpService(ExpService), ILevelTableProvider — RewardDispatcher가 IExpService 의존
        services.AddGameServices();
        services.AddContentServices();

        // UnitOfWork가 IAsyncDisposable만 구현하므로 await using으로 비동기 정리
        await using var provider = services.BuildServiceProvider();

        // RewardDispatcher Resolve가 throw 없이 성공해야 함
        var dispatcher = provider.GetRequiredService<IRewardDispatcher>();
        Assert.NotNull(dispatcher);
    }

    [Fact]
    public async Task ExpService_CanBeResolved()
    {
        // 부분 DI 컨테이너 + InMemory DbContext
        var services = TestServiceProviderBuilder.CreateBaseServices();

        // 운영 저장소 등록
        // - AddAuthRepositories: IPlayerProfileRepository (ExpService 직접 의존)
        services.AddAuthRepositories();
        services.AddGameRepositories();

        // 운영 서비스 등록
        // - AddContentServices: IExpService(ExpService), ILevelTableProvider, IUnitOfWork
        // - AddGameServices: IUnitOfWork (ExpService 의존)
        services.AddGameServices();
        services.AddContentServices();

        // UnitOfWork가 IAsyncDisposable만 구현하므로 await using으로 비동기 정리
        await using var provider = services.BuildServiceProvider();

        // IExpService Resolve가 throw 없이 성공해야 함
        var expService = provider.GetRequiredService<IExpService>();
        Assert.NotNull(expService);
    }
}
