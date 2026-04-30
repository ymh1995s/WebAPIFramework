using Microsoft.AspNetCore.Diagnostics;

namespace Framework.Api.Middleware;

/// <summary>
/// 처리되지 않은 모든 예외에 대한 폴백 핸들러.
/// EnumDeserializationExceptionHandler가 처리하지 않은 예외를 받아
/// 500 응답을 반환한다. 개발 환경에서는 예외를 그대로 전파하여
/// 개발자 도구(스택 트레이스 등)에서 확인할 수 있도록 허용한다.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // 개발 환경에서는 처리하지 않음 — 예외가 개발자 도구로 전파되도록 허용
        if (_env.IsDevelopment()) return false;

        // 요청 경로와 메서드를 함께 기록하여 어느 API 호출이 실패했는지 추적
        _logger.LogError(exception, "[API 오류] {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        // 클라이언트에는 내부 정보를 노출하지 않고 일반 오류 응답만 반환
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync("{\"message\":\"서버 내부 오류가 발생했습니다.\"}", cancellationToken);

        return true;
    }
}
