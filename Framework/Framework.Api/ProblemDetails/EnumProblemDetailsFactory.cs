using Framework.Api.Json;
using Microsoft.AspNetCore.Mvc;

namespace Framework.Api.ProblemDetails;

/// <summary>
/// EnumDeserializationException을 RFC 7807 형식의 ProblemDetails로 변환하는 정적 팩토리.
/// expectedType은 개발 환경에서만 포함되어 내부 타입 정보가 프로덕션에 노출되지 않도록 한다.
/// </summary>
public static class EnumProblemDetailsFactory
{
    /// <summary>
    /// enum 역직렬화 오류에 대한 ProblemDetails 응답 객체를 생성한다.
    /// </summary>
    /// <param name="ex">발생한 EnumDeserializationException</param>
    /// <param name="instance">요청 경로 (ProblemDetails.Instance 필드)</param>
    /// <param name="isDevelopment">개발 환경 여부 — true일 때만 expectedType 포함</param>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails Build(
        EnumDeserializationException ex,
        string instance,
        bool isDevelopment)
    {
        // 오류 상세 항목 구성 — field, receivedValue, allowedValues는 항상 포함
        var errorEntry = new Dictionary<string, object?>
        {
            ["field"]         = ex.Field,
            ["receivedValue"] = ex.ReceivedValue,
            ["allowedValues"] = ex.AllowedValues,
        };

        // expectedType은 개발 환경에서만 추가 (프로덕션에서는 내부 타입명 미노출)
        if (isDevelopment)
        {
            errorEntry["expectedType"] = ex.ExpectedType;
        }

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type     = "https://framework.api/errors/invalid-enum",
            Title    = "Invalid enum value",
            Status   = StatusCodes.Status400BadRequest,
            Detail   = "The request contains one or more invalid enum values.",
            Instance = instance,
        };

        // extensions["errors"] 배열로 오류 목록 전달
        problem.Extensions["errors"] = new[] { errorEntry };

        return problem;
    }
}
