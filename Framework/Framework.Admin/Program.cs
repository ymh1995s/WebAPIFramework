using Framework.Admin.Components;
using Framework.Admin.Handlers;
using Framework.Admin.Http;
using Framework.Admin.Logging;
using Framework.Admin.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using System.Security.Claims;

// ─────────────────────────────────────────────────────────────
// Serilog 설정
//
// UseSerilog()를 호출하면 ASP.NET Core 기본 로거(ILogger<T>)가
// Serilog로 교체된다. 즉, 컴포넌트에서 ILogger<T>를 주입받아도
// 실제로는 Serilog가 처리한다.
//
// [릴리즈 빌드 전용 파일 로그]
// - 경로: logs/admin-.log (날짜별 롤링, 예: admin-20260423.log)
// - 보관: 최대 30일 / 파일 1개당 최대 50MB
// - Debug 빌드에서는 콘솔 로그만 출력하여 파일 I/O 오버헤드를 제거
//
// [왜 파일 로그를 DB보다 우선하는가]
// DB 장애 자체가 크래시 원인인 경우, DB에 로그를 쓰는 시도도 실패한다.
// 파일은 DB와 독립적으로 동작하므로 어떤 상황에서도 기록이 남는다.
// ─────────────────────────────────────────────────────────────
// --hash <비밀번호> 인자 실행 시 BCrypt 해시 출력 후 종료 — 운영 비밀번호 설정 도구
if (args.Length >= 2 && args[0] == "--hash")
{
    Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(args[1], workFactor: 12));
    return;
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // 개발/운영 공통: 콘솔 출력
    .WriteTo.Console()
#if !DEBUG
    // 릴리즈 빌드 전용: 파일 롤링 로그
    .WriteTo.File(
        path: "logs/admin-.log",          // 날짜별 파일명 자동 생성
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,        // 30일치 보관
        fileSizeLimitBytes: 50 * 1024 * 1024, // 파일당 최대 50MB
        rollOnFileSizeLimit: true)         // 크기 초과 시 새 파일 생성
#endif
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ASP.NET Core 기본 로거를 Serilog로 교체
builder.Host.UseSerilog();

// Razor 컴포넌트 및 인터랙티브 서버 렌더링 등록
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Cookie 기반 인증 등록 - 미인증 시 /login으로 리다이렉트
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// 인가 서비스 등록 — FallbackPolicy로 어노테이션 누락 페이지도 기본 인증 필수 적용 (Fail-safe)
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

// HTTP 로그 저장소 — Singleton으로 모든 컴포넌트가 동일 인스턴스를 공유
builder.Services.AddSingleton<IHttpLogStore, InMemoryHttpLogStore>();

// BCrypt 기반 Admin 비밀번호 검증기 등록
builder.Services.AddSingleton<IAdminPasswordVerifier, AdminPasswordVerifier>();

// X-Admin-Key 헤더 자동 주입 핸들러 등록
builder.Services.AddTransient<AdminApiKeyHandler>();
// HTTP 로그 캡처 핸들러 — AdminApiKeyHandler 다음에 체인으로 삽입
builder.Services.AddTransient<HttpLogCaptureHandler>();
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7034");
})
.AddHttpMessageHandler<AdminApiKeyHandler>()
.AddHttpMessageHandler<HttpLogCaptureHandler>();

// ApiHttpClient 래퍼 — 모든 API 호출에 AdminJsonOptions(camelCase enum) 적용
builder.Services.AddScoped<ApiHttpClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();

// Debug 빌드 전용 자동 로그인 - Release 빌드에서는 컴파일 제외
#if DEBUG
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "dev-admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        context.User = principal;

        if (!context.WebSockets.IsWebSocketRequest)
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
    await next();
});
#endif

app.UseAuthorization();

// 로그인 처리 엔드포인트 - BCrypt 해시 검증 후 인증 쿠키 발급
app.MapPost("/admin-login", async (HttpContext context, IAdminPasswordVerifier verifier, [Microsoft.AspNetCore.Mvc.FromForm] string password) =>
{
    if (!verifier.Verify(password))
        return Results.Redirect("/login?error=1");

    var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.Redirect("/players");
}).DisableAntiforgery().AllowAnonymous();

// 루트 경로를 첫 번째 페이지로 리다이렉트
app.MapGet("/", () => Results.Redirect("/players")).AllowAnonymous();

// 로그아웃 엔드포인트 - 쿠키 삭제 후 로그인 페이지로 이동
app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
