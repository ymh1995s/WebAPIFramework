using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Framework.Api.Filters;

// 구글 계정 연동 필수 Authorization Filter — 게스트 계정의 결제 진입 차단
// GoogleId가 연동된 계정만 접근 허용, 게스트(GoogleId == null)는 403 반환
//
// [사용 방법] 결제 등 유료 기능 엔드포인트에 [Authorize]와 함께 부착:
//   [Authorize]
//   [RequireLinkedAccount]
//   [HttpPost("purchase")]
//   public async Task<IActionResult> Purchase(...) { ... }
//
// [동작 원리] IAsyncAuthorizationFilter 인터페이스를 구현하면 ASP.NET Core가
// 요청 파이프라인의 인증 단계에서 OnAuthorizationAsync()를 자동 호출함
// 직접 호출하는 코드는 없고 어트리뷰트를 붙이는 것만으로 동작함
public class RequireLinkedAccountAttribute : Attribute, IAsyncAuthorizationFilter
{
    // 인증/인가 단계에서 실행 — 컨트롤러 액션보다 먼저 호출됨
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // JWT 클레임에서 playerId 추출
        var playerIdClaim = context.HttpContext.User.FindFirst("playerId");
        if (playerIdClaim is null || !int.TryParse(playerIdClaim.Value, out var playerId))
        {
            // 인증 자체가 안 된 경우 — [Authorize] 필터가 먼저 잡아야 하지만 방어적으로 처리
            context.Result = new UnauthorizedResult();
            return;
        }

        // DB에서 현재 플레이어의 GoogleId 조회
        var playerRepo = context.HttpContext.RequestServices.GetRequiredService<IPlayerRepository>();
        var player = await playerRepo.GetByIdAsync(playerId);

        if (player is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // GoogleId가 없으면 게스트 계정 → 결제/유료 기능 접근 차단
        if (player.GoogleId is null)
        {
            context.Result = new ObjectResult(new
            {
                error = "GUEST_PURCHASE_BLOCKED",
                message = "구글 계정 연동 후 결제가 가능합니다."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        // GoogleId가 있으면 정상 통과 (context.Result 미설정 시 다음 단계로 진행)
        await Task.CompletedTask;
    }
}
