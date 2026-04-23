namespace Framework.Admin.Handlers;

// API 서버로의 모든 HTTP 요청에 X-Admin-Key 헤더를 자동으로 주입하는 핸들러
public class AdminApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _config;

    public AdminApiKeyHandler(IConfiguration config)
    {
        _config = config;
    }

    // Blazor에서 HttpClient로 API 요청이 나갈 때마다 이 메서드가 먼저 실행됨
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Framework.Admin/appsettings.json의 Admin:ApiKey 값을 읽어
        // 모든 요청 헤더에 "X-Admin-Key: {값}" 형태로 자동 추가
        // → API 서버 점검 미들웨어에서 이 헤더를 확인해 Admin 요청으로 판별, 503 면제
        var apiKey = _config["Admin:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("X-Admin-Key", apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
