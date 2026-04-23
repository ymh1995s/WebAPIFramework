using Microsoft.AspNetCore.Components.Web;

namespace Framework.Admin.Components;

/// <summary>
/// Blazor 내장 <see cref="ErrorBoundary"/>를 상속하여 Serilog 로깅만 추가한 래퍼.
///
/// [이 컴포넌트가 하는 일]
/// ErrorBoundary는 자식 컴포넌트 트리에서 발생한 모든 예외
/// (렌더링 / 라이프사이클 / 이벤트 핸들러)를 자동으로 catch하여
/// SignalR 회로가 끊기는 것을 막고, ErrorContent로 대체 UI를 표시한다.
/// 기본 구현은 예외를 삼킬 뿐이라 어떤 오류가 났는지 흔적이 남지 않으므로
/// OnErrorAsync를 오버라이드하여 Serilog 파일 로그에 기록한다.
///
/// [왜 Release에서만 사용하는가]
/// Debug에서는 예외를 그대로 터뜨려야 개발자가 즉시 원인을 파악할 수 있다.
/// 따라서 MainLayout에서 `#if !DEBUG`로 감싸 릴리즈 빌드에서만 활성화한다.
/// </summary>
public class SafeErrorBoundary : ErrorBoundary
{
    // 부모 ErrorBoundary도 내부적으로 ILogger를 사용하지만 private 필드라
    // 외부에서 접근 불가. 우리는 별도로 주입받아 명확한 태그로 기록한다.
    [Microsoft.AspNetCore.Components.Inject]
    private ILogger<SafeErrorBoundary> Logger { get; set; } = default!;

    /// <summary>
    /// 자식 컴포넌트에서 예외가 발생하면 Blazor가 이 메서드를 호출한다.
    /// 기본 동작(에러 UI 전환)은 base.OnErrorAsync에서 처리되는 것이 아니라
    /// ErrorBoundaryBase.HandleExceptionAsync 내부에서 처리되므로
    /// 이 오버라이드는 로깅만 담당하면 충분하다.
    /// </summary>
    protected override Task OnErrorAsync(Exception exception)
    {
        // 예외 타입 + 메시지 + 스택트레이스를 파일에 남긴다.
        // 페이지/컴포넌트 단위 식별은 스택트레이스로 확인 가능.
        Logger.LogError(exception, "[Admin 오류] ErrorBoundary가 예외를 catch했습니다");
        return Task.CompletedTask;
    }
}
