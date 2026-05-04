using Framework.Api.ProblemDetails;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.Middleware;

/// <summary>
/// 처리되지 않은 모든 예외에 대한 폴백 핸들러.
/// EnumDeserializationExceptionHandler가 처리하지 않은 예외를 받아
/// 500 ProblemDetails 응답을 반환한다. 개발 환경에서는 예외를 그대로 전파하여
/// 개발자 도구(스택 트레이스 등)에서 확인할 수 있도록 허용한다.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IWebHostEnvironment env,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _env = env;
        _problemDetailsService = problemDetailsService;
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

        // 클라이언트에는 내부 정보를 노출하지 않고 RFC 7807 ProblemDetails 응답 반환 (M-13)
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type   = "https://framework.api/errors/internal",
                Title  = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "서버 내부 오류가 발생했습니다.",
                Extensions = { ["errorCode"] = ErrorCodes.InternalError }
            }
        });

        return true;
    }
}
