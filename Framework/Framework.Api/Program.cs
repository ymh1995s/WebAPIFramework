using Framework.Api.Extensions;
using Framework.Api.Hubs;
using Framework.Api.ProblemDetails;
using Framework.Api.Security;
using Framework.Application.Features.SystemConfig;
using Framework.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text.Json;

// ─────────────────────────────────────────────────────────────
// Serilog 설정
//
// [API는 왜 컴포넌트별 try-catch가 불필요한가]
// ASP.NET Core 컨트롤러는 요청마다 독립적으로 실행된다(stateless).
// 한 요청에서 예외가 발생해도 다음 요청은 영향을 받지 않는다.
// 따라서 전역 예외 미들웨어 하나로 모든 처리를 중앙화한다.
//
// [파일 경로 분리]
// Admin 로그(logs/admin-.log)와 API 로그(logs/api-.log)를 분리하여
// 문제 발생 시 어느 서버에서 발생했는지 즉시 구분 가능하다.
// ─────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // 요청 단위 컨텍스트(LogContext.PushProperty 등) 전파에 필수
    .Enrich.FromLogContext()
    // 컨테이너/노드 식별 — Docker 멀티 인스턴스 환경에서 어느 노드인지 구분
    .Enrich.WithMachineName()
    // Development/Production 구분 — 동일 Sink 공유 시 환경 식별 (ASPNETCORE_ENVIRONMENT 환경변수 사용)
    .Enrich.WithEnvironmentName()
    // 멀티 앱(Api/Admin) 로그 통합 시 필터링용 고정 속성
    .Enrich.WithProperty("Application", "Framework.Api")
    .WriteTo.Console()
#if !DEBUG
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 50 * 1024 * 1024,
        rollOnFileSizeLimit: true)
#endif
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ASP.NET Core 기본 로거를 Serilog로 교체
builder.Host.UseSerilog();

// ── DB ────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql =>
    {
        // EF Core 일시 단절 자동 재시도 — connection drop, admin shutdown 등 transient 예외만 대상
        // 외부 도메인 retry 루프(RewardDispatcher/MailService의 DbUpdateConcurrencyException 3회)와는
        // 트리거 예외가 달라 중첩 발생 안 함 (xmin 충돌은 비-transient)
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    }));

// ── 헬스체크 ──────────────────────────────────────────────────
// 외부 probe(컨테이너 오케스트레이터, 모니터링)용 — DB 연결 상태 포함
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ── 저장소 ────────────────────────────────────────────────────
builder.Services.AddAuthRepositories();      // 인증 (Player, RefreshToken)
builder.Services.AddNoticeRepositories();    // 공지 저장소
builder.Services.AddShoutRepositories();     // 1회 공지 저장소
builder.Services.AddInquiryRepositories();   // 문의 저장소
builder.Services.AddAdRewardRepositories();  // 광고 보상 저장소
builder.Services.AddIapRepositories();       // 인앱결제 저장소
builder.Services.AddContentRepositories();   // 컨텐츠(스테이지) 저장소
builder.Services.AddGameRepositories();      // 게임 결과/매치 저장소

// ── 서비스 ────────────────────────────────────────────────────
builder.Services.AddAuthServices();          // 인증 (로그인, OAuth, 보상 파이프라인 등)
builder.Services.AddAdminServices();         // Admin 운영 서비스 (우편, 시스템 설정 등)
builder.Services.AddGameServices();          // 게임 서비스 (매치 결과, 랭킹 등)
builder.Services.AddNoticeServices();        // 공지 서비스
builder.Services.AddShoutServices();         // 1회 공지 서비스
builder.Services.AddInquiryServices();       // 문의 서비스
builder.Services.AddMatchmakingServices(builder.Configuration); // 매치메이킹 (SignalR)
builder.Services.AddAdRewardServices();      // 광고 SSV (UnityAds, IronSource)
builder.Services.AddIapServices();           // 인앱결제 (Google Play)
builder.Services.AddContentServices();       // 게임 컨텐츠 (스테이지 등)

// ── 인증 / 보안 ───────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimitingServices(builder.Configuration);
builder.Services.AddMemoryCache();          // 점검 모드 등 설정 캐시

// Admin Key 검증기 — Singleton: 시작 시점 1회 인코딩, 타이밍 공격 방어
builder.Services.AddSingleton<IAdminKeyValidator, AdminKeyValidator>();

// JSON 직렬화 옵션 설정
// - EnumMemberJsonConverterFactory: 잘못된 enum 값 수신 시 EnumDeserializationException을 발생시켜 400 반환
// - camelCase: 클라이언트(Unity/JS)에서 관례적으로 사용하는 camelCase 프로퍼티명 적용
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Framework.Api.Json.EnumMemberJsonConverterFactory());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// enum 역직렬화 오류 → 400 ProblemDetails 변환, ModelState 포맷 통일
builder.Services.AddApiErrorHandling();

// OpenAPI(Swagger) 문서 생성
builder.Services.AddOpenApi();

var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// DB 마이그레이션 자동 적용
//
// [목적]
// Docker 컨테이너가 시작될 때 대기 중인 EF Core 마이그레이션을
// 자동으로 DB에 반영한다. 최초 기동 시 테이블이 생성되고
// 이후 마이그레이션이 추가될 때마다 재기동만으로 스키마가 최신화된다.
//
// [주의]
// 소규모 운영 기준의 편의 기능이다. 유저가 크게 늘어나면
// 무중단 배포 전략과 충돌할 수 있으므로 수동 적용으로 전환 고려.
// ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// 개발 환경에서만 OpenAPI 엔드포인트 활성화
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 개발 환경에서는 모바일 테스트를 위해 HTTPS 리다이렉트 비활성화
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// OWASP 권장 보안 응답 헤더 6종 부착 — OnStarting 콜백으로 모든 응답에 보장
app.UseSecurityHeaders(app.Environment);

// ─────────────────────────────────────────────────────────────
// 전역 예외 핸들러 활성화 — 파이프라인 최상단에 위치해야
// 이후 모든 미들웨어(인증, 점검, 컨트롤러 등)의 예외를 포착할 수 있다.
//
// [처리 순서]
// 1. EnumDeserializationExceptionHandler — enum 역직렬화 오류 → 400 ProblemDetails
// 2. GlobalExceptionHandler              — 그 외 모든 예외  → 500 (프로덕션 전용)
// ─────────────────────────────────────────────────────────────
app.UseExceptionHandler();

// Rate Limiter는 인증보다 앞에 위치해야 함
app.UseRateLimiter();

// 점검 모드 미들웨어 — X-Admin-Key 헤더가 있는 요청(Admin Blazor)은 통과, 나머지는 수동/예약 여부 확인 후 503 반환
app.Use(async (context, next) =>
{
    var validator = context.RequestServices.GetRequiredService<IAdminKeyValidator>();
    var adminKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
    var isAdminRequest = validator.IsValid(adminKey);

    if (!isAdminRequest)
    {
        var configService = context.RequestServices.GetRequiredService<ISystemConfigService>();
        if (await configService.IsUnderMaintenanceAsync())
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"서버 점검 중입니다. 잠시 후 다시 시도해주세요.\"}");
            return;
        }
    }
    await next();
});

// 인증 → 인가 순서 중요
app.UseAuthentication();

#if DEBUG
// 디버그 빌드 전용 - 릴리즈 빌드에서는 이 코드가 컴파일되지 않음
// PlayerId = 1 로 고정된 가짜 인증을 주입하여 토큰 없이 API 테스트 가능
// 적용 대상: Swagger, Admin 어드민 직접 호출 등 토큰 없이 플레이어 API를 테스트할 때
// 미적용 대상: Admin > 문의 관리 > 문의 테스트처럼 실제 Bearer 토큰을 발급·사용하는 시나리오
app.Use(async (context, next) =>
{
    // 이미 인증된 요청(Bearer 토큰 등)은 건드리지 않음 — 미인증 요청에만 우회 적용
    if (context.User?.Identity?.IsAuthenticated != true)
    {
        var claims = new[] { new System.Security.Claims.Claim("playerId", "1") };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "DebugBypass");
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);
    }
    await next();
});
#endif

// 모든 HTTP 요청을 한 줄 구조화 로그로 기록 — 인증 이후 위치이므로 PlayerId 클레임 사용 가능
// 인증/인가 결과(401/403)와 일반 4xx/5xx 응답은 자동 캡처. 단 점검 모드 503·RateLimiter 429는
// 본 미들웨어 앞에서 short-circuit(next 미호출)되므로 캡처 대상 외 — 필요 시 해당 미들웨어에서 별도 로깅
app.UseSerilogRequestLogging(options =>
{
    // 로그 메시지 형식: HTTP 메서드, 경로, 상태 코드, 응답 시간(ms)
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0}ms)";

    // /health 200은 Verbose(sink 미출력) — probe 반복 호출 노이즈 제거
    // /health 503은 Warning — DB 단절 가시화
    // 그 외 예외는 Error, 나머지 기본 Information
    options.GetLevel = (ctx, elapsed, ex) =>
    {
        if (ex != null) return Serilog.Events.LogEventLevel.Error;
        if (ctx.Request.Path.StartsWithSegments("/health"))
            return ctx.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Verbose;
        return Serilog.Events.LogEventLevel.Information;
    };

    // 요청별 추가 컨텍스트 속성 삽입
    options.EnrichDiagnosticContext = (diag, ctx) =>
    {
        // 클라이언트 IP 주소
        diag.Set("ClientIp", ctx.Connection.RemoteIpAddress?.ToString());
        // 클라이언트 User-Agent (Unity SDK 버전 식별 등에 활용)
        diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString());
        // ASP.NET Core 요청 추적 식별자 — 분산 추적 연계용
        diag.Set("TraceId", ctx.TraceIdentifier);
        // 인증된 요청만 PlayerId 기록 — 미인증 요청은 null
        diag.Set("PlayerId", ctx.User.GetPlayerId());
    };
});

app.UseAuthorization();

app.MapControllers();

// 외부 probe 진입점 — JWT/AdminKey 없이 호출 가능, JSON 본문으로 의존성별 상태 반환
// RateLimit 정책 및 인증 정책 미부착 (AllowAnonymous 효과)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        // 프록시/CDN 캐시 방지
        context.Response.Headers.CacheControl = "no-store";

        // 의존성별 상태를 JSON 형식으로 직렬화
        // description 필드는 EF DbContextCheck 기본값 그대로 — ConnectionString 등 민감정보 미포함
        var json = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });

        await context.Response.WriteAsync(json);
    }
});

// SignalR 허브 엔드포인트 등록
app.MapHub<MatchMakingHub>("/hubs/matchmaking");

app.Run();
