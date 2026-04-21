using Framework.Application.Interfaces;

namespace Framework.Api.Services;

// 매일 00:00 일일 보상 자동 발송 스케줄러
public class DailyRewardScheduler : BackgroundService
{
    // ASP.NET Core 내장 - DI 컨테이너에서 Scoped 서비스를 수동으로 꺼낼 때 사용
    private readonly IServiceProvider _serviceProvider;
    // ASP.NET Core 내장 - 로그 출력용
    private readonly ILogger<DailyRewardScheduler> _logger;

    public DailyRewardScheduler(IServiceProvider serviceProvider, ILogger<DailyRewardScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // BackgroundService 추상 클래스의 추상 메서드 - 앱 시작 시 ASP.NET Core 호스트가 자동으로 호출
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // 다음 자정까지 대기
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;

            await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDailyLoginService>();
                await service.ProcessDailyRewardForAllAsync();
                _logger.LogInformation("일일 보상 자동 발송 완료: {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "일일 보상 자동 발송 중 오류 발생");
            }
        }
    }
}
