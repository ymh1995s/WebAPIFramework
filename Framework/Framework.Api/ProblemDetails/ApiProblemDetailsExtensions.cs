using Framework.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.ProblemDetails;

/// <summary>
/// API 에러 처리 서비스 등록 확장 메서드.
/// ProblemDetails 포맷을 전체 프로젝트에서 통일하기 위해 한 곳에서 설정한다.
/// </summary>
public static class ApiProblemDetailsExtensions
{
    /// <summary>
    /// ProblemDetails 서비스 및 EnumDeserializationExceptionHandler를 DI에 등록한다.
    /// ModelState 400 응답도 동일한 ProblemDetails 포맷으로 통일하기 위해
    /// ApiBehaviorOptions.InvalidModelStateResponseFactory를 재정의한다.
    /// </summary>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        // RFC 7807 ProblemDetails 서비스 등록
        // CustomizeProblemDetails: traceId·instance를 모든 ProblemDetails 응답에 자동 부착 (M-13)
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                // traceId — 로그와의 연계 식별자. 이미 있으면 덮어쓰지 않음
                ctx.ProblemDetails.Extensions["traceId"] ??= ctx.HttpContext.TraceIdentifier;

                // instance — 오류가 발생한 요청 경로. 이미 있으면 덮어쓰지 않음
                ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
            };
        });

        // enum 역직렬화 예외 전용 핸들러 등록 (400 반환)
        services.AddExceptionHandler<EnumDeserializationExceptionHandler>();

        // 처리되지 않은 모든 예외에 대한 폴백 핸들러 등록 (500 반환, 프로덕션 전용)
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // ModelState 400 응답 포맷을 ProblemDetails로 통일
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                // enum 외 일반 ModelState 오류는 기존 ValidationProblemDetails 동작 유지
                // 단, type/title/status 필드만 프레임워크 기본값 그대로 사용하여 포맷 일관성 확보
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Type   = "https://framework.api/errors/validation",
                    Title  = "Validation failed",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = context.HttpContext.Request.Path,
                };

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }
}
