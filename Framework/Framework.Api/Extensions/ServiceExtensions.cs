using System.Text;
using System.Threading.RateLimiting;
using Framework.Application.Common;
using Framework.Application.Features.AdPolicy;
using Framework.Application.Features.AdReward;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Auth;
using Framework.Application.Features.DailyLogin;
using Framework.Application.Features.DailyReward;
using Framework.Application.Features.Inquiry;
using Framework.Application.Features.Item;
using Framework.Application.Features.Mail;
using Framework.Application.Features.Matchmaking;
using Framework.Application.Features.Notice;
using Framework.Application.Features.AdminMatch;
using Framework.Application.Features.AdminPlayer;
using Framework.Application.Features.Ranking;
using Framework.Application.Features.Reward;
using Framework.Application.Features.RewardGrant;
using Framework.Application.Features.RewardTable;
using Framework.Application.Features.SystemConfig;
using Framework.Domain.Constants;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Framework.Infrastructure.Repositories;
using Framework.Api.Notifications;
using Framework.Api.Services;
using Framework.Api.Services.AdNetwork;
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
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IPlayerItemRepository, PlayerItemRepository>();
        services.AddScoped<IMailRepository, MailRepository>();
        services.AddScoped<IRankingRepository, RankingRepository>();
        services.AddScoped<IDailyLoginLogRepository, DailyLoginLogRepository>();
        services.AddScoped<IDailyRewardSlotRepository, DailyRewardSlotRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // 보상 프레임워크 저장소 등록
        services.AddScoped<IRewardGrantRepository, RewardGrantRepository>();
        services.AddScoped<IGameResultRepository, GameResultRepository>();
        services.AddScoped<IRewardTableRepository, RewardTableRepository>();

        // 광고 보상 저장소 등록
        services.AddScoped<IAdPolicyRepository, AdPolicyRepository>();

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

    // 저장소 등록 - 문의
    public static IServiceCollection AddInquiryRepositories(this IServiceCollection services)
    {
        services.AddScoped<IInquiryRepository, InquiryRepository>();
        return services;
    }

    // 서비스 등록 - Admin 플레이어 관리
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminPlayerService, AdminPlayerService>();

        // 보상 프레임워크 Admin 서비스 등록
        services.AddScoped<IRewardTableService, RewardTableService>();
        services.AddScoped<IRewardGrantQueryService, RewardGrantQueryService>();
        services.AddScoped<IAdminMatchService, AdminMatchService>();

        // 광고 정책 Admin 서비스 등록
        services.AddScoped<IAdPolicyService, AdPolicyService>();

        return services;
    }

    // 서비스 등록 - 인게임 관련
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddScoped<IMailService, MailService>();
        services.AddScoped<IDailyLoginService, DailyLoginService>();
        services.AddScoped<IDailyRewardSlotService, DailyRewardSlotService>();
        services.AddScoped<IPlayerItemService, PlayerItemService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IItemMasterService, ItemMasterService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // 보상 디스패처 등록 — 모든 보상 경로의 단일 진입점
        services.AddScoped<IRewardDispatcher, RewardDispatcher>();

        // 작업 단위 등록 — 보상 지급 트랜잭션 원자성 보장
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    // 서비스 등록 - 공지
    public static IServiceCollection AddNoticeServices(this IServiceCollection services)
    {
        services.AddScoped<INoticeService, NoticeService>();
        return services;
    }

    // 서비스 등록 - 문의
    public static IServiceCollection AddInquiryServices(this IServiceCollection services)
    {
        services.AddScoped<IInquiryService, InquiryService>();
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

    // Rate Limiter 정책 등록 — 한도값은 appsettings.json RateLimiting 섹션에서 관리
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        // RateLimitLogRepository는 OnRejected 콜백에서 직접 생성하므로 Scoped 등록
        services.AddScoped<RateLimitLogRepository>();

        // appsettings.json의 RateLimiting:AuthPermitLimit 값 사용 — 미설정 시 60 기본값
        var authPermitLimit = config.GetValue<int>("RateLimiting:AuthPermitLimit", 60);

        // 광고 콜백 Rate Limit — 광고 네트워크 서버 IP 기준 분당 요청 수 제한
        // 미설정 시 300 기본값 (광고 네트워크 서버는 합법적으로 많은 요청 발송)
        var adsCallbackPermitLimit = config.GetValue<int>("RateLimiting:AdsCallbackPermitLimit", 300);

        services.AddRateLimiter(options =>
        {
            // AddFixedWindowLimiter: 이름("auth")을 붙여 등록하는 방식
            // [EnableRateLimiting("auth")]로 특정 컨트롤러/액션에만 선택 적용 가능
            // 로그인 API처럼 별도 제한이 필요한 엔드포인트에 사용
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = authPermitLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // 광고 SSV 콜백 Rate Limit — 광고 네트워크 서버가 직접 호출하는 엔드포인트
            // DDoS/어뷰징 방지용, 정상적인 광고 네트워크 트래픽보다 충분히 높게 설정
            options.AddFixedWindowLimiter("ads-callback", limiter =>
            {
                limiter.PermitLimit = adsCallbackPermitLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // HTTP 429 Too Many Requests — Rate Limit 초과 시 서버가 반환하는 상태 코드
            // 현재 적용 정책: auth 엔드포인트(/auth/*) IP 기준 authPermitLimit회/분
            // 발생 즉시 RateLimitLog DB에 기록하여 보안 감시 페이지에서 확인 가능
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
                    // JWT 클레임에서 PlayerId 추출 — 인증된 요청이면 설정, 비인증이면 null
                    int? playerId = null;
                    var playerIdClaim = context.HttpContext.User.FindFirst("playerId")?.Value;
                    if (int.TryParse(playerIdClaim, out var parsedId))
                        playerId = parsedId;

                    // User-Agent 256자 제한 — 봇 탐지 보조
                    var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
                    if (userAgent.Length > 256) userAgent = userAgent[..256];

                    var repo = context.HttpContext.RequestServices
                        .GetRequiredService<RateLimitLogRepository>();
                    await repo.AddAsync(new RateLimitLog
                    {
                        IpAddress = ip,
                        Path = path,
                        Policy = policy,
                        OccurredAt = DateTime.UtcNow,
                        PlayerId = playerId,
                        UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent
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

    // 광고 SSV(Server Side Verification) 서비스 등록
    // 검증기는 IAdNetworkVerifier를 구현하는 모든 타입이 IEnumerable로 주입됨 (Strategy 패턴)
    public static IServiceCollection AddAdRewardServices(this IServiceCollection services)
    {
        // 검증기 전략 구현체 등록 (새 네트워크 추가 시 여기에 한 줄만 추가)
        services.AddScoped<IAdNetworkVerifier, UnityAdsVerifier>();
        services.AddScoped<IAdNetworkVerifier, IronSourceVerifier>();

        // 검증기 팩토리 (Resolver) 등록
        services.AddScoped<IAdNetworkVerifierResolver, AdNetworkVerifierResolver>();

        // 광고 보상 처리 서비스 등록
        services.AddScoped<IAdRewardService, AdRewardService>();

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
