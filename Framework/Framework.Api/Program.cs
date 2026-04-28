using Framework.Api.Extensions;
using Framework.Api.Hubs;
using Framework.Application.Features.SystemConfig;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

// DB 컨텍스트 등록
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 저장소 등록
builder.Services.AddAuthRepositories();
builder.Services.AddGameRepositories();
builder.Services.AddNoticeRepositories();
builder.Services.AddInquiryRepositories();

// 서비스 등록
builder.Services.AddAuthServices();
builder.Services.AddAdminServices();
builder.Services.AddGameServices();
builder.Services.AddNoticeServices();
builder.Services.AddInquiryServices();
builder.Services.AddMatchMakingServices(builder.Configuration);

// JWT 인증 설정
builder.Services.AddJwtAuthentication(builder.Configuration);

// Rate Limiter 정책 등록
builder.Services.AddRateLimiting();

// 메모리 캐시 — 점검 모드 등 매 요청마다 확인되는 설정 조회 비용을 줄이기 위해 사용
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
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

// Rate Limiter는 인증보다 앞에 위치해야 함
app.UseRateLimiter();

// 점검 모드 미들웨어 — X-Admin-Key 헤더가 있는 요청(Admin Blazor)은 통과, 나머지는 수동/예약 여부 확인 후 503 반환
app.Use(async (context, next) =>
{
    var adminKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
    var expectedKey = context.RequestServices.GetRequiredService<IConfiguration>()["Admin:ApiKey"];
    var isAdminRequest = !string.IsNullOrEmpty(adminKey) && adminKey == expectedKey;

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
app.Use(async (context, next) =>
{
    var claims = new[] { new System.Security.Claims.Claim("playerId", "1") };
    var identity = new System.Security.Claims.ClaimsIdentity(claims, "DebugBypass");
    context.User = new System.Security.Claims.ClaimsPrincipal(identity);
    await next();
});
#endif

app.UseAuthorization();

// ─────────────────────────────────────────────────────────────
// 전역 예외 처리 미들웨어 (릴리즈 빌드 전용)
//
// [동작 방식]
// 컨트롤러에서 처리되지 않은 예외가 이 미들웨어까지 버블링되면
// Serilog로 기록한 뒤 클라이언트에게 500을 반환한다.
// 스택 트레이스 등 내부 정보는 절대 클라이언트에 노출하지 않는다.
// ─────────────────────────────────────────────────────────────
#if !DEBUG
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // 요청 경로와 메서드를 함께 기록하여 어느 API 호출이 실패했는지 추적
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "[API 오류] {Method} {Path}", context.Request.Method, context.Request.Path);

        // 클라이언트에는 내부 정보를 노출하지 않고 일반 오류 응답만 반환
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"message\":\"서버 내부 오류가 발생했습니다.\"}");
    }
});
#endif

app.MapControllers();

// SignalR 허브 엔드포인트 등록
app.MapHub<MatchMakingHub>("/hubs/matchmaking");

app.Run();
