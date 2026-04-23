using Framework.Api.Extensions;
using Framework.Api.Hubs;
using Framework.Application.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DB 컨텍스트 등록
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 저장소 등록
builder.Services.AddAuthRepositories();
builder.Services.AddGameRepositories();

// 서비스 등록
builder.Services.AddAuthServices();
builder.Services.AddGameServices();
builder.Services.AddMatchMakingServices(builder.Configuration);

// JWT 인증 설정
builder.Services.AddJwtAuthentication(builder.Configuration);

// Rate Limiter 정책 등록
builder.Services.AddRateLimiting();

// 스케줄러 등록 (매일 00:00 자동 발송)
builder.Services.AddHostedService<Framework.Api.Services.DailyRewardScheduler>();

builder.Services.AddControllers();
// OpenAPI(Swagger) 문서 생성
builder.Services.AddOpenApi();

var app = builder.Build();

// 개발 환경에서만 OpenAPI 엔드포인트 활성화
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.MapControllers();

// SignalR 허브 엔드포인트 등록
app.MapHub<MatchMakingHub>("/hubs/matchmaking");

app.Run();
