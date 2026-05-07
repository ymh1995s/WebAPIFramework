using Framework.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

namespace Framework.Api.Middleware;

// AuthDomainExceptionHandler가 처리하지 못한 DomainException을 400으로 일괄 처리.
// AuthDomainExceptionHandler 다음에 등록해야 Auth 401 흐름이 유지됨.
// 응답 포맷은 AuthDomainExceptionHandler와 동일한 RFC 7807 ProblemDetails 형식 사용.
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // DomainException 계열이 아니면 처리하지 않음 — 다음 핸들러에 위임
        if (exception is not DomainException domainEx)
            return false;

        // 응답이 이미 시작된 경우 추가 쓰기 시도하지 않음 — 호스트 크래쉬 방지
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning("응답 시작 후 변환 불가 — Path: {Path}", httpContext.Request.Path);
            return false;
        }

        // 도메인 규칙 위반 로그 기록 — 원인 추적용
        _logger.LogWarning(
            "도메인 예외 — Path: {Path}, ErrorCode: {ErrorCode}, Message: {Message}",
            httpContext.Request.Path, domainEx.ErrorCode, domainEx.Message);

        httpContext.Response.StatusCode  = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        // RFC 7807 ProblemDetails 형식으로 응답 — AuthDomainExceptionHandler와 동일 스키마 유지
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, new
        {
            type      = "https://framework.api/errors/bad-request",
            title     = "Bad Request",
            status    = StatusCodes.Status400BadRequest,
            detail    = domainEx.Message,
            instance  = httpContext.Request.Path.ToString(),
            errorCode = domainEx.ErrorCode,
            traceId   = httpContext.TraceIdentifier
        }, cancellationToken: cancellationToken);

        return true;
    }
}
