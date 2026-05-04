using System.Text.Json;
using Framework.Api.Json;
using Framework.Api.ProblemDetails;
using Microsoft.AspNetCore.Diagnostics;

namespace Framework.Api.Middleware;

/// <summary>
/// EnumDeserializationException을 포착하여 400 ProblemDetails 응답으로 변환하는 예외 핸들러.
/// ASP.NET Core IExceptionHandler 인터페이스를 구현하며 UseExceptionHandler() 파이프라인에서 동작한다.
/// </summary>
public class EnumDeserializationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<EnumDeserializationExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IWebHostEnvironment _env;

    public EnumDeserializationExceptionHandler(
        ILogger<EnumDeserializationExceptionHandler> logger,
        IProblemDetailsService problemDetailsService,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _env = env;
    }

    /// <summary>
    /// 예외가 EnumDeserializationException이거나 그 InnerException인 경우 처리한다.
    /// 다른 예외 타입은 false를 반환하여 파이프라인의 다음 핸들러로 넘긴다.
    /// traceId·instance 자동 부착은 ApiProblemDetailsExtensions의 CustomizeProblemDetails가 담당한다.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // EnumDeserializationException 자체이거나 InnerException에 포함된 경우 추출
        EnumDeserializationException? enumEx = exception as EnumDeserializationException
            ?? exception.InnerException as EnumDeserializationException;

        // 해당 예외가 아니면 다음 핸들러(GlobalExceptionHandler)로 위임
        if (enumEx is null)
            return false;

        // JsonException.Path가 있으면 field에 주입 (어느 필드에서 오류가 발생했는지 명확하게 표시)
        if (exception is JsonException jsonEx && !string.IsNullOrEmpty(jsonEx.Path))
        {
            enumEx.SetField(jsonEx.Path);
        }

        // 서버 로그에만 기록 — 스택 트레이스는 클라이언트에 미노출
        _logger.LogWarning(
            "enum 역직렬화 오류 — Field: {Field}, ReceivedValue: {ReceivedValue}, ExpectedType: {ExpectedType}",
            enumEx.Field,
            enumEx.ReceivedValue,
            enumEx.ExpectedType
        );

        // 팩토리에서 ProblemDetails 생성 — errorCode·errors 배열 포함
        // instance는 팩토리에서 직접 설정하므로 CustomizeProblemDetails의 ??= 와 중복 없음
        var problemDetails = EnumProblemDetailsFactory.Build(
            enumEx,
            instance: httpContext.Request.Path.ToString(),
            isDevelopment: _env.IsDevelopment()
        );

        // 응답 상태 코드 400 설정 후 ProblemDetails 직렬화 전송
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext    = httpContext,
            ProblemDetails = problemDetails,
        });

        return true;
    }
}
