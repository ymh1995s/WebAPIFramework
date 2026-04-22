using Framework.Admin.Components;
using Framework.Admin.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

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

// 인가 서비스 등록
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// X-Admin-Key 헤더 자동 주입 핸들러 등록
builder.Services.AddTransient<AdminApiKeyHandler>();
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5058");
}).AddHttpMessageHandler<AdminApiKeyHandler>();

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

// 로그인 처리 엔드포인트 - 비밀번호 검증 후 인증 쿠키 발급
app.MapPost("/admin-login", async (HttpContext context, IConfiguration config, [Microsoft.AspNetCore.Mvc.FromForm] string password) =>
{
    if (password != config["Admin:Password"])
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
