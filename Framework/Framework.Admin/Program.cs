using Framework.Admin.Components;
using Framework.Admin.Extensions;
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
    // 요청 단위 컨텍스트(LogContext.PushProperty 등) 전파에 필수
    .Enrich.FromLogContext()
    // 컨테이너/노드 식별 — Docker 멀티 인스턴스 환경에서 어느 노드인지 구분
    .Enrich.WithMachineName()
    // Development/Production 구분 — 동일 Sink 공유 시 환경 식별 (ASPNETCORE_ENVIRONMENT 환경변수 사용)
    .Enrich.WithEnvironmentName()
    // 멀티 앱(Api/Admin) 로그 통합 시 필터링용 고정 속성
    .Enrich.WithProperty("Application", "Framework.Admin")
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

// 라이브 안정성 #4 — AppDomain 최후 hook. 모든 보호 우회한 unhandled 예외 캡처
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var ex = e.ExceptionObject as Exception;
    Log.Fatal(
        ex,
        "[FATAL] AppDomain unhandled exception. IsTerminating={IsTerminating} ProcessId={ProcessId} Machine={Machine}",
        e.IsTerminating,
        Environment.ProcessId,
        Environment.MachineName);
    Log.CloseAndFlush();
};

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

// 인가 서비스 등록 — FallbackPolicy=RequireAuthenticatedUser 복원
// 어노테이션 없는 신규 비-컴포넌트 엔드포인트(예: 향후 추가될 Minimal API)를 자동 차단하는 안전망.
// 컴포넌트 엔드포인트(MapRazorComponents·/_blazor SignalR 회로)와 정적자산(MapStaticAssets)은
// 명시적 AllowAnonymous로 FallbackPolicy 대상에서 제외 → 백색화면 재발 방지.
// 페이지 단위 보호는 _Imports.razor 전역 [Authorize] + AuthorizeRouteView 가 별도 담당(컴포넌트 레이어).
builder.Services.AddAuthorization(options =>
{
    // 비-컴포넌트 엔드포인트 안전망: AllowAnonymous 명시 없는 신규 엔드포인트는 자동 인증 요구
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
// HttpLog 캡처 제외 경로 — 30초 폴링 컴포넌트 노이즈 차단
builder.Services.Configure<HttpLogCaptureOptions>(o =>
    o.ExcludedPaths = new[] { Framework.Admin.Constants.ApiRoutes.Health.Endpoint });
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7034");
})
.AddHttpMessageHandler<AdminApiKeyHandler>()
.AddHttpMessageHandler<HttpLogCaptureHandler>();

// ApiHttpClient 래퍼 — 모든 API 호출에 AdminJsonOptions(camelCase enum) 적용
builder.Services.AddScoped<ApiHttpClient>();

var app = builder.Build();

// 정상 종료 (Ctrl+C / SIGTERM 등) 시 Serilog 비동기 sink 버퍼 flush 보장
app.Lifetime.ApplicationStopped.Register(() => Log.CloseAndFlush());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // UseHsts() 제거 — HSTS는 UseSecurityHeaders 미들웨어에서 일원화하여 관리
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// OWASP 권장 보안 응답 헤더 6종 부착 — OnStarting 콜백으로 모든 응답에 보장
app.UseSecurityHeaders(app.Environment);

app.UseAuthentication();

// 자동 로그인 — Debug 또는 LoadTest 심볼 정의 시 활성화, Release(둘 다 미정의)는 컴파일 제외
// Api 프로젝트의 인증 우회 정책(#if DEBUG || LOADTEST)과 대칭 구성.
// LoadTest = 격리된 부하테스트 환경에서 로그인 절차 없이 Admin 접속을 허용하기 위한 심볼.
#if DEBUG || LOADTEST
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
#endif // DEBUG || LOADTEST

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

// 정적자산/Blazor 프레임워크 전송 채널은 인증 대상 아님 —
// FallbackPolicy(RequireAuthenticatedUser)가 _framework/*.js, collocated *.razor.js, css 등을
// 차단하면 Release/LoadTest 빌드에서 백색화면 발생. 페이지·SignalR 회로 보호는 별도 유지 (Phase 2).
app.MapStaticAssets().AllowAnonymous();
// MapRazorComponents(초기 SSR + /_blazor SignalR 회로)를 FallbackPolicy 대상에서 명시 제외.
// FallbackPolicy가 복원된 상태에서도 Blazor 회로 연결이 차단되지 않도록 AllowAnonymous 적용.
// 페이지 보호는 _Imports.razor [Authorize] + AuthorizeRouteView 컴포넌트 레이어가 담당.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

// API 서버가 먼저 기동될 수 있도록 1초 대기 후 시작
await Task.Delay(TimeSpan.FromSeconds(1));

app.Run();
