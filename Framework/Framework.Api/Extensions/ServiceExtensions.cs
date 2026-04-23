using System.Text;
using System.Threading.RateLimiting;
using Framework.Application.Interfaces;
using Framework.Application.Options;
using Framework.Application.Services;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Repositories;
using Framework.Api.Notifications;
using Framework.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Framework.Api.Extensions;

// Program.cs 서비스 등록을 역할별로 분리한 확장 메서드 모음
public static class ServiceExtensions
{
    // 저장소 등록 - 인증 관련
    public static IServiceCollection AddAuthRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        return services;
    }

    // 저장소 등록 - 인게임 관련
    public static IServiceCollection AddGameRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRecordRepository, PlayerRecordRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IPlayerItemRepository, PlayerItemRepository>();
        services.AddScoped<IMailRepository, MailRepository>();
        services.AddScoped<IRankingRepository, RankingRepository>();
        services.AddScoped<IDailyLoginLogRepository, DailyLoginLogRepository>();
        services.AddScoped<IDailyRewardConfigRepository, DailyRewardConfigRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        return services;
    }

    // 서비스 등록 - 인증 관련
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenProvider, JwtTokenProvider>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        return services;
    }

    // 저장소 등록 - 공지
    public static IServiceCollection AddNoticeRepositories(this IServiceCollection services)
    {
        services.AddScoped<INoticeRepository, NoticeRepository>();
        return services;
    }

    // 서비스 등록 - 인게임 관련
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRecordService, PlayerRecordService>();
        services.AddScoped<IMailService, MailService>();
        services.AddScoped<IDailyLoginService, DailyLoginService>();
        services.AddScoped<IPlayerItemService, PlayerItemService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IItemMasterService, ItemMasterService>();
        return services;
    }

    // 서비스 등록 - 공지
    public static IServiceCollection AddNoticeServices(this IServiceCollection services)
    {
        services.AddScoped<INoticeService, NoticeService>();
        return services;
    }

    // 서비스 등록 - 매칭 관련
    public static IServiceCollection AddMatchMakingServices(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("MatchMaking").Get<MatchMakingOptions>() ?? new MatchMakingOptions();
        services.AddSingleton(options);
        services.AddSignalR();
        services.AddSingleton<IMatchNotifier, SignalRMatchNotifier>();
        services.AddSingleton<IMatchMakingService, MatchMakingService>();
        return services;
    }

    // Rate Limiter 정책 등록
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        // RateLimitLogRepository는 OnRejected 콜백에서 직접 생성하므로 Scoped 등록
        services.AddScoped<RateLimitLogRepository>();

        services.AddRateLimiter(options =>
        {
            // AddFixedWindowLimiter: 이름("auth")을 붙여 등록하는 방식
            // [EnableRateLimiting("auth")]로 특정 컨트롤러/액션에만 선택 적용 가능
            // 로그인 API처럼 별도 제한이 필요한 엔드포인트에 사용
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // GlobalLimiter: 이름 없이 모든 요청에 자동 적용되는 기본 정책
            // [EnableRateLimiting] 어트리뷰트 없이도 전체 API에 깔리며
            // IP 기준으로 요청자를 직접 구분(파티션)하는 로직을 작성해야 함
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // 429 발생 시 DB에 로그 기록
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var path = context.HttpContext.Request.Path.ToString();
                var policy = context.HttpContext.GetEndpoint()
                    ?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "global";

                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<RateLimitLog>>();
                logger.LogWarning("Rate limit 초과 — IP: {Ip}, Path: {Path}, Policy: {Policy}", ip, path, policy);

                try
                {
                    var repo = context.HttpContext.RequestServices
                        .GetRequiredService<RateLimitLogRepository>();
                    await repo.AddAsync(new RateLimitLog
                    {
                        IpAddress = ip,
                        Path = path,
                        Policy = policy,
                        OccurredAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Rate limit 로그 DB 저장 실패");
                }
            };
        });

        return services;
    }

    // JWT 인증 설정
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwtKey = config["Jwt:SecretKey"]!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
        return services;
    }
}
