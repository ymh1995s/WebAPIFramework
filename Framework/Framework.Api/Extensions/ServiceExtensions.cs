using System.Text;
using System.Threading.RateLimiting;
using Framework.Application.Common;
using Framework.Application.Features.AdPolicy;
using Framework.Application.Features.AdReward;
using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.BanLog;
using Framework.Application.Features.Auth;
using Framework.Application.Features.DailyLogin;
using Framework.Application.Features.DailyReward;
using Framework.Application.Features.Iap;
using Framework.Application.Features.IapProduct;
using Framework.Application.Features.Inquiry;
using Framework.Application.Features.Item;
using Framework.Application.Features.Mail;
using Framework.Application.Features.Matchmaking;
using Framework.Application.Features.Notice;
using Framework.Application.Features.Shout;
using Framework.Application.Features.AdminMatch;
using Framework.Application.Features.AdminPlayer;
using Framework.Application.Features.Ranking;
using Framework.Application.Features.Reward;
using Framework.Application.Features.RewardGrant;
using Framework.Application.Features.RewardTable;
using Framework.Application.Features.SystemConfig;
using Framework.Application.Features.Exp;
using Framework.Application.Content.Stage;
using Framework.Domain.Constants;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Domain.Content.Interfaces;
using Framework.Infrastructure.Persistence;
using Framework.Infrastructure.Repositories;
using Framework.Infrastructure.Content.Repositories;
using Framework.Api.BackgroundServices;
using Framework.Api.Notifications;
using Framework.Api.Services;
using Framework.Api.Services.AdNetwork;
using Framework.Api.Services.IapStore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Framework.Api.Extensions;

// Program.cs 서비스 등록을 역할별로 분리한 확장 메서드 모음
// 배치 순서: 저장소 그룹 → 서비스 그룹 → 인증/인프라 그룹
public static class ServiceExtensions
{
    // ──────────────────────────────────────────────────────────────
    // 저장소 그룹
    // ──────────────────────────────────────────────────────────────

    // 저장소 등록 - 인증 관련
    public static IServiceCollection AddAuthRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        return services;
    }

    // 저장소 등록 - 공지
    public static IServiceCollection AddNoticeRepositories(this IServiceCollection services)
    {
        services.AddScoped<INoticeRepository, NoticeRepository>();
        return services;
    }

    // 저장소 등록 - 1회 공지
    public static IServiceCollection AddShoutRepositories(this IServiceCollection services)
    {
        services.AddScoped<IShoutRepository, ShoutRepository>();
        return services;
    }

    // 저장소 등록 - 문의
    public static IServiceCollection AddInquiryRepositories(this IServiceCollection services)
    {
        services.AddScoped<IInquiryRepository, InquiryRepository>();
        return services;
    }

    // 저장소 등록 - 광고 보상 관련
    public static IServiceCollection AddAdRewardRepositories(this IServiceCollection services)
    {
        services.AddScoped<IAdPolicyRepository, AdPolicyRepository>();
        return services;
    }

    // 저장소 등록 - 인앱결제 관련
    public static IServiceCollection AddIapRepositories(this IServiceCollection services)
    {
        services.AddScoped<IIapProductRepository, IapProductRepository>();
        services.AddScoped<IIapPurchaseRepository, IapPurchaseRepository>();
        // Admin 알림 저장소 등록
        services.AddScoped<IAdminNotificationRepository, AdminNotificationRepository>();
        return services;
    }

    // 저장소 등록 - 컨텐츠(스테이지) 관련
    public static IServiceCollection AddContentRepositories(this IServiceCollection services)
    {
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<IStageClearRepository, StageClearRepository>();
        return services;
    }

    // 저장소 등록 - 인게임 공통 관련
    // (광고/IAP/컨텐츠 저장소는 각 전용 메서드로 분리됨)
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

        // 레벨 임계값 저장소 등록
        services.AddScoped<ILevelThresholdRepository, LevelThresholdRepository>();

        return services;
    }

    // ──────────────────────────────────────────────────────────────
    // 서비스 그룹
    // ──────────────────────────────────────────────────────────────

    // 서비스 등록 - 인증 관련
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenProvider, JwtTokenProvider>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        return services;
    }

    // 서비스 등록 - Admin 플레이어/보상/매치 관리
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminPlayerService, AdminPlayerService>();

        // 밴 이력 저장소 및 서비스 등록
        services.AddScoped<IBanLogRepository, BanLogRepository>();
        services.AddScoped<IBanLogService, BanLogService>();

        // 보상 프레임워크 Admin 서비스 등록
        services.AddScoped<IRewardTableService, RewardTableService>();
        services.AddScoped<IRewardGrantQueryService, RewardGrantQueryService>();
        services.AddScoped<IAdminMatchService, AdminMatchService>();

        // 광고 정책 Admin 서비스 등록
        services.AddScoped<IAdPolicyService, AdPolicyService>();

        // 인앱결제 Admin 서비스 등록
        services.AddScoped<IIapProductService, IapProductService>();
        services.AddScoped<IIapPurchaseAdminService, IapPurchaseAdminService>();

        // Admin 알림 서비스 등록
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();

        return services;
    }

    // 서비스 등록 - 인게임 공통 관련
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

    // 서비스 등록 - 1회 공지
    public static IServiceCollection AddShoutServices(this IServiceCollection services)
    {
        services.AddScoped<IShoutService, ShoutService>();
        return services;
    }

    // 서비스 등록 - 문의
    public static IServiceCollection AddInquiryServices(this IServiceCollection services)
    {
        services.AddScoped<IInquiryService, InquiryService>();
        return services;
    }

    // 서비스 등록 - 매칭 관련
    public static IServiceCollection AddMatchmakingServices(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("MatchMaking").Get<MatchMakingOptions>() ?? new MatchMakingOptions();
        services.AddSingleton(options);
        services.AddSignalR();
        services.AddSingleton<IMatchNotifier, SignalRMatchNotifier>();
        services.AddSingleton<IMatchMakingService, MatchMakingService>();
        return services;
    }

    // 서비스 등록 - 광고 SSV(Server Side Verification)
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

    // 서비스 등록 - 인앱결제(IAP)
    // Google Play 검증기 + 메인 구매 처리 서비스 + consume 재시도 워커
    public static IServiceCollection AddIapServices(this IServiceCollection services)
    {
        // Google Play 클라이언트 팩토리 — Verifier/Consumer 공유 초기화 (Transient: 요청마다 새 클라이언트)
        services.AddTransient<GooglePlayClientFactory>();

        // Google Play 영수증 검증기 등록 (IIapStoreVerifier Strategy 구현체)
        services.AddScoped<IIapStoreVerifier, GooglePlayStoreVerifier>();

        // 스토어 검증기 팩토리 (Resolver) 등록
        services.AddScoped<IIapStoreVerifierResolver, IapStoreVerifierResolver>();

        // Google Play consume 구현체 등록 (IIapConsumer Strategy)
        services.AddScoped<IIapConsumer, GooglePlayConsumer>();

        // consume 해석자 등록 — 스토어별 IIapConsumer 구현체 분기
        services.AddScoped<IIapConsumerResolver, IapConsumerResolver>();

        // 인앱결제 메인 검증+지급 서비스 등록
        services.AddScoped<IIapPurchaseService, IapPurchaseService>();

        // RTDN 알림 처리 서비스 등록
        services.AddScoped<IIapRtdnService, IapRtdnService>();

        // Google Pub/Sub OIDC 검증기 — Singleton: 내부 JWKS ConfigurationManager 캐시 공유
        services.AddSingleton<GooglePubSubAuthenticator>();

        // consume 재시도 백그라운드 서비스 — 일시실패 건 폴링 + 지수 백오프 재시도
        services.AddHostedService<IapConsumeRetryService>();

        return services;
    }

    // 서비스 등록 - 게임 컨텐츠 (스테이지 클리어, 경험치)
    public static IServiceCollection AddContentServices(this IServiceCollection services)
    {
        // 레벨 테이블 제공자 — Singleton 등록: 프로세스 전역 캐시 공유
        services.AddSingleton<ILevelTableProvider, LevelTableProvider>();

        // 레벨 테이블 Admin 서비스 등록
        services.AddScoped<ILevelTableAdminService, LevelTableAdminService>();

        // 경험치 서비스 등록 — 스테이지 클리어 Exp 지급의 단일 진입점
        services.AddScoped<IExpService, ExpService>();

        // 스테이지 클리어 서비스 등록
        services.AddScoped<IStageClearService, StageClearService>();

        return services;
    }

    // ──────────────────────────────────────────────────────────────
    // 인증/인프라 그룹
    // ──────────────────────────────────────────────────────────────

    // JWT 인증 설정 — appsettings.json Jwt 섹션의 값을 읽어 Bearer 토큰 검증 파라미터를 구성
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwtKey = config["Jwt:SecretKey"]!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // SignalR WebSocket 연결 시 쿼리스트링 access_token을 Bearer 토큰으로 사용
                // 브라우저 WebSocket API는 임의 헤더 부착 불가 — 표준 SignalR 토큰 전달 방식
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // 발급자(iss 클레임)가 appsettings의 Jwt:Issuer와 일치하는지 검증
                    ValidateIssuer = true,

                    // 수신자(aud 클레임)가 appsettings의 Jwt:Audience와 일치하는지 검증
                    ValidateAudience = true,

                    // 만료 시각(exp 클레임) 검증 — 만료된 토큰 자동 거부
                    ValidateLifetime = true,

                    // 서명 키 검증 — 위·변조 토큰 차단 (HMAC-SHA256)
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
        return services;
    }

    // Rate Limiter 정책 등록 — 한도값은 appsettings.json RateLimiting 섹션에서 관리
    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration config)
    {
        // RateLimitLogRepository는 OnRejected 콜백에서 직접 생성하므로 Scoped 등록
        services.AddScoped<RateLimitLogRepository>();

        // 인증 엔드포인트 Rate Limit 한도
        // AuthPermitLimit: 미인증(IP) 기준 분당 허용 횟수 — 미설정 시 15 기본값
        // AuthPlayerPermitLimit: 인증(PlayerId) 기준 분당 허용 횟수 — 미설정 시 30 기본값
        var authPermitLimit = config.GetValue<int>("RateLimiting:AuthPermitLimit", 15);
        var authPlayerPermitLimit = config.GetValue<int>("RateLimiting:AuthPlayerPermitLimit", 30);

        // 광고 콜백 Rate Limit — 광고 네트워크 서버 IP 기준 분당 요청 수 제한
        // 미설정 시 300 기본값 (광고 네트워크 서버는 합법적으로 많은 요청 발송)
        var adsCallbackPermitLimit = config.GetValue<int>("RateLimiting:AdsCallbackPermitLimit", 300);

        // RTDN 수신 Rate Limit — Google Pub/Sub 재시도 정책을 감안하여 충분히 높게 설정
        // 미설정 시 600 기본값 (Google은 비-200 응답 시 최대 7일간 재시도)
        var iapRtdnPermitLimit = config.GetValue<int>("RateLimiting:IapRtdnPermitLimit", 600);

        // 인게임 API 공통 Rate Limit — 정상 플레이 기준 분당 최대 ~33회 → 3.5배 여유
        var gamePermitLimit = config.GetValue<int>("RateLimiting:GamePermitLimit", 120);

        // IAP 결제 검증 전용 Rate Limit — 정상 결제는 분당 수회, 봇 결제 시도 차단
        var iapVerifyPermitLimit = config.GetValue<int>("RateLimiting:IapVerifyPermitLimit", 20);

        services.AddRateLimiter(options =>
        {
            // 인증 엔드포인트 파티션 정책 — 인증 여부에 따라 파티션 키·한도를 분기
            // 인증 성공(JWT 유효): PlayerId 기준 파티셔닝 → 분당 authPlayerPermitLimit회
            // 미인증/토큰 없음:   IP 기준 파티셔닝     → 분당 authPermitLimit회
            // [EnableRateLimiting("auth")]로 AuthController 전체에 적용
            options.AddPolicy("auth", httpContext =>
            {
                var playerId = httpContext.User.GetPlayerId();

                // 인증 여부에 따라 파티션 키 결정
                // 인증 시: player:{id}, 미인증 시: ip:{RemoteIpAddress}
                var key = playerId.HasValue
                    ? $"player:{playerId.Value}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        // 인증 플레이어는 관대하게, 미인증 IP는 엄격하게 제한
                        PermitLimit = playerId.HasValue ? authPlayerPermitLimit : authPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
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

            // Google Pub/Sub RTDN 수신 Rate Limit — Google 서버가 직접 호출
            // 재시도 감안하여 충분히 높게 설정, 과도한 트래픽 차단 목적
            options.AddFixedWindowLimiter("iap-rtdn", limiter =>
            {
                limiter.PermitLimit = iapRtdnPermitLimit;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // 인게임 API 공통 정책 — PlayerId 기준 파티셔닝, 미인증 시 IP fallback
            options.AddPolicy("game", httpContext =>
            {
                var playerId = httpContext.User.GetPlayerId();
                var key = playerId.HasValue
                    ? $"player:{playerId.Value}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = gamePermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // IAP 결제 검증 전용 정책 — 봇 결제 시도 차단, 정상 유저는 분당 20회 이내
            options.AddPolicy("iap-verify", httpContext =>
            {
                var playerId = httpContext.User.GetPlayerId();
                var key = playerId.HasValue
                    ? $"player:{playerId.Value}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = iapVerifyPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // HTTP 429 Too Many Requests — Rate Limit 초과 시 서버가 반환하는 상태 코드
            // auth 정책: 미인증 IP 분당 authPermitLimit회 / 인증 PlayerId 분당 authPlayerPermitLimit회
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
                    var playerId = context.HttpContext.User.GetPlayerId();

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
                    // 변경 추적 후 명시적 저장 — SaveChanges 패턴 통일
                    await repo.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Rate limit 로그 DB 저장 실패");
                }
            };
        });

        return services;
    }
}
