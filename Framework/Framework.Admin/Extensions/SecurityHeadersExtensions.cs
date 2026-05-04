// OWASP 권장 보안 응답 헤더 미들웨어 — 모든 응답에 부착 (예외/점검/RateLimit 포함)
// CSP는 Admin Blazor 깨짐 위험 회피를 위해 별도 라운드에서 도입 예정
// Api와 동일한 6종 헤더를 적용. 향후 CSP 추가 시 두 파일이 갈라질 예정
namespace Framework.Admin.Extensions;

public static class SecurityHeadersExtensions
{
    /// <summary>
    /// OWASP 권장 보안 응답 헤더 6종을 모든 응답에 부착하는 미들웨어를 등록한다.
    /// OnStarting 콜백을 사용하므로 예외/점검/RateLimit 등 short-circuit 응답에도 보장된다.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        IWebHostEnvironment env)
    {
        return app.Use(async (context, next) =>
        {
            // OnStarting — 응답 송신 직전에 헤더 최종 부착 (다른 미들웨어가 덮어쓰지 못함)
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // MIME-Sniffing 차단 — 브라우저가 응답 콘텐츠 타입을 자체 추측하지 않게
                headers["X-Content-Type-Options"] = "nosniff";

                // Clickjacking 차단 — 외부 사이트 iframe 임베드 거부
                headers["X-Frame-Options"] = "DENY";

                // Referrer 정보 제한 — 외부 사이트로 토큰/쿼리 누출 방지
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                // 브라우저 기능 차단 — Admin은 카메라/마이크/지오로케이션 등 사용 안 함
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";

                // Adobe Flash/PDF 등 cross-domain 정책 차단 (역사적 호환성)
                headers["X-Permitted-Cross-Domain-Policies"] = "none";

                // HTTPS 강제 — Production 환경만 (Development는 HTTPS 미사용 가능)
                if (env.IsProduction())
                {
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            });

            await next();
        });
    }
}
