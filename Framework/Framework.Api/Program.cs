using Framework.Api.Extensions;
using Framework.Api.Hubs;
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

// 인증 → 인가 순서 중요
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR 허브 엔드포인트 등록
app.MapHub<MatchMakingHub>("/hubs/matchmaking");

app.Run();
