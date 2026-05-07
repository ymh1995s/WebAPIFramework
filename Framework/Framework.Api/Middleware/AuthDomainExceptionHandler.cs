using System.Text.Json;
using Framework.Application.Features.Auth.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Framework.Api.Middleware;

// AuthDomainException 계열을 401 ProblemDetails로 변환하는 핸들러.
// IProblemDetailsService를 사용하지 않고 직접 JSON 직렬화로 응답을 완결하여
// Development 환경 포함 모든 환경에서 안정적으로 동작한다.
public class AuthDomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AuthDomainExceptionHandler> _logger;

    public AuthDomainExceptionHandler(ILogger<AuthDomainExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // 도메인 인증 예외만 처리 — .NET 프레임워크의 UnauthorizedAccessException은 GlobalExceptionHandler에서 500으로 처리됨
        if (exception is not AuthDomainException authEx)
            return false;

        // 응답이 이미 시작된 경우 추가 쓰기 시도하지 않음 — 호스트 크래쉬 방지
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning("응답 시작 후 401 변환 불가 — Path: {Path}", httpContext.Request.Path);
            return false;
        }

        // 서버 로그에 기록 — 인증 실패 원인 추적용
        _logger.LogWarning(
            "인증 실패 — Path: {Path}, ErrorCode: {ErrorCode}, Message: {Message}",
            httpContext.Request.Path, authEx.ErrorCode, authEx.Message);

        // RFC 7807 ProblemDetails 형식으로 응답 직렬화
        // anonymous object 사용 이유: ProblemDetails.Extensions는 JsonSerializer로 중첩 직렬화되어
        // Unity 클라이언트의 ProblemDetailsDto(errorCode 루트 필드)와 구조가 맞지 않음
        httpContext.Response.StatusCode  = StatusCodes.Status401Unauthorized;
        httpContext.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, new
        {
            type      = "https://framework.api/errors/unauthorized",
            title     = "Unauthorized",
            status    = StatusCodes.Status401Unauthorized,
            detail    = authEx.Message,
            instance  = httpContext.Request.Path.ToString(),
            errorCode = authEx.ErrorCode,
            traceId   = httpContext.TraceIdentifier
        }, cancellationToken: cancellationToken);

        return true;
    }
}
