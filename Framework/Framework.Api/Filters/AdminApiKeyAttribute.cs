using Framework.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Framework.Api.Filters;

// Admin API Key 검증 어트리뷰트 — X-Admin-Key 헤더를 타이밍 안전 방식으로 검증
public class AdminApiKeyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // IAdminKeyValidator를 통해 타이밍 공격 안전 검증 수행
        var validator = context.HttpContext.RequestServices.GetRequiredService<IAdminKeyValidator>();
        var key = context.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault();

        if (!validator.IsValid(key))
            context.Result = new ForbidResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
