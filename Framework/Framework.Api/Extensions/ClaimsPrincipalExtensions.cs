using System.Security.Claims;

namespace Framework.Api.Extensions;

// ClaimsPrincipal 확장 메서드 — JWT 클레임에서 PlayerId를 안전하게 추출하는 헬퍼
// 컨트롤러, 필터, 미들웨어 등 User(ClaimsPrincipal) 접근이 가능한 모든 곳에서 사용 가능
public static class ClaimsPrincipalExtensions
{
    // JWT playerId 클레임에서 int 변환 — 클레임이 없거나 파싱 실패 시 null 반환 (안전 버전)
    public static int? GetPlayerId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("playerId");
        if (claim is null) return null;
        if (int.TryParse(claim.Value, out var id)) return id;
        return null;
    }

    // JWT playerId 클레임에서 int 변환 — [Authorize] 보호 하에서 반드시 존재할 때 사용
    // 클레임이 없거나 파싱 실패 시 InvalidOperationException 발생
    public static int GetPlayerIdRequired(this ClaimsPrincipal user)
        => GetPlayerId(user) ?? throw new InvalidOperationException("playerId 클레임이 없습니다.");
}
