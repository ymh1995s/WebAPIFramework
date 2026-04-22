using System.Text;
using Framework.Application.Interfaces;
using Framework.Application.Options;
using Framework.Application.Services;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Repositories;
using Framework.Api.Notifications;
using Framework.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

    // 서비스 등록 - 인게임 관련
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRecordService, PlayerRecordService>();
        services.AddScoped<IMailService, MailService>();
        services.AddScoped<IDailyLoginService, DailyLoginService>();
        services.AddScoped<IPlayerItemService, PlayerItemService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IRankingService, RankingService>();
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
