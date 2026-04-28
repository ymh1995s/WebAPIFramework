using Microsoft.AspNetCore.Components;
using System.Diagnostics;

namespace Framework.Admin.Components.Pages.Errors;

/// <summary>
/// 오류 페이지 코드-비하인드.
/// 요청 ID를 표시하여 오류 추적을 지원한다.
/// </summary>
public partial class Error : ComponentBase
{
    /// <summary>SSR 컴포넌트에서 HttpContext 접근 — TraceIdentifier 확인용</summary>
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized() =>
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
}
