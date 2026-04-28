using Microsoft.AspNetCore.Components;

// Routes.razor가 Pages.NotFound로 직접 참조하므로
// namespace는 Pages 수준을 유지해야 한다.
namespace Framework.Admin.Components.Pages;

/// <summary>
/// 404 Not Found 페이지 코드-비하인드.
/// 단순 정적 페이지이므로 빈 partial class로 구성된다.
/// </summary>
public partial class NotFound : ComponentBase
{
}
