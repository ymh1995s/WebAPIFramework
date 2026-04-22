using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Framework.Api.Filters;

// Admin API Key 검증 어트리뷰트 - 컨트롤러나 메서드에 붙이면 자동으로 X-Admin-Key 헤더 검증
public class AdminApiKeyAttribute : Attribute, IResourceFilter
{
    // 요청이 컨트롤러에 도달하기 전에 실행
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // 어트리뷰트는 DI 컨테이너 밖에서 생성되므로 직접 서비스를 꺼내야 함
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        // 요청 헤더에서 X-Admin-Key 값 추출 (없으면 null)
        var key = context.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault();

        // appsettings.json의 Admin:ApiKey 값 (정답 키)
        // [현재] appsettings.json에 하드코딩된 임시 키 사용
        // [라이브] 환경변수 또는 Blazor Admin 전용 JWT 인증으로 교체 필요
        var expectedKey = config["Admin:ApiKey"];

        // 키가 없거나 정답과 다르면 context.Result에 403을 세팅
        // → ASP.NET Core가 이 값을 보고 컨트롤러 실행을 중단하고 403 반환
        if (string.IsNullOrEmpty(key) || key != expectedKey)
            context.Result = new ForbidResult();
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
