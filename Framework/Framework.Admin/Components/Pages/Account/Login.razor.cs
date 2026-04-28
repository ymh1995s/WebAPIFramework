using Microsoft.AspNetCore.Components;

namespace Framework.Admin.Components.Pages.Account;

/// <summary>
/// Admin 로그인 페이지 코드-비하인드.
/// SSR 컴포넌트에서 HttpContext 쿼리스트링을 통해 로그인 실패 여부를 판단한다.
/// </summary>
public partial class Login : ComponentBase
{
    /// <summary>SSR 컴포넌트에서 HttpContext 접근 - 쿼리스트링 확인용</summary>
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    /// <summary>?error=1 쿼리스트링이 있으면 오류 메시지 표시</summary>
    private bool showError => HttpContext?.Request.Query.ContainsKey("error") == true;
}
