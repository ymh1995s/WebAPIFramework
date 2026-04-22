namespace Framework.Admin.Handlers;

// API 서버로의 모든 HTTP 요청에 X-Admin-Key 헤더를 자동으로 주입하는 핸들러
public class AdminApiKeyHandler : DelegatingHandler
{
    private readonly IConfiguration _config;

    public AdminApiKeyHandler(IConfiguration config)
    {
        _config = config;
    }

    // 요청 전송 전 Admin API 키 헤더 추가
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _config["Admin:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("X-Admin-Key", apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
