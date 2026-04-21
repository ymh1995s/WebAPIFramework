using Framework.Application.Interfaces;
using Framework.Application.Services;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Framework.Infrastructure.Repositories;
using Framework.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DI 서비스 등록
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 저장소 등록
builder.Services.AddScoped<IPlayerRecordRepository, PlayerRecordRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IPlayerItemRepository, PlayerItemRepository>();
builder.Services.AddScoped<IMailRepository, MailRepository>();
builder.Services.AddScoped<IDailyLoginLogRepository, DailyLoginLogRepository>();
builder.Services.AddScoped<IDailyRewardConfigRepository, DailyRewardConfigRepository>();
builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();

// 서비스 등록
builder.Services.AddScoped<IPlayerRecordService, PlayerRecordService>();
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<IDailyLoginService, DailyLoginService>();
builder.Services.AddScoped<IPlayerItemService, PlayerItemService>();
builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();

// 스케줄러 등록 (매일 00:00 자동 발송)
builder.Services.AddHostedService<DailyRewardScheduler>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
