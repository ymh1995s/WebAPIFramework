using Microsoft.AspNetCore.Components;

namespace Framework.Admin.Components;

/// <summary>
/// 모든 Admin Blazor 페이지 컴포넌트의 베이스 클래스.
///
/// [왜 이 클래스가 필요한가]
/// Blazor Server는 SignalR 회로(circuit) 위에서 동작한다.
/// 컴포넌트의 이벤트 핸들러나 라이프사이클 메서드에서 예외가 발생하고
/// 그 예외를 아무도 잡지 않으면, ASP.NET Core 런타임이 SignalR 회로를 끊어버린다.
/// 회로가 끊기면 해당 페이지의 모든 버튼/이벤트가 먹통이 되고,
/// 사용자는 새로고침 없이 복구할 수 없다.
///
/// [전략]
/// - DEBUG 빌드: 예외를 그대로 던진다. 개발 중에 문제를 즉시 발견할 수 있다.
/// - RELEASE 빌드: try-catch로 예외를 잡아 Serilog 파일 로그에 기록하고,
///   화면에는 사용자 친화적인 오류 메시지만 표시한다. 회로는 유지된다.
///
/// [사용법]
/// @inherits SafeComponentBase
/// ...
/// await SafeExecute(async () => { ... });
/// </summary>
public abstract class SafeComponentBase : ComponentBase
{
    // ILogger는 Blazor 컴포넌트에서 [Inject]로 주입받는다.
    // 베이스 클래스에서 선언하면 모든 하위 컴포넌트가 자동으로 사용 가능하다.
    [Inject]
    protected ILogger<SafeComponentBase> Logger { get; set; } = default!;

    /// <summary>
    /// 비동기 작업을 안전하게 실행하는 래퍼 메서드.
    ///
    /// [errorMessage 파라미터]
    /// 예외 발생 시 화면에 표시할 오류 메시지를 담는 string? 변수의 참조를 받는다.
    /// ref를 사용하는 이유: 이 메서드가 호출자의 변수를 직접 수정해야 하기 때문이다.
    /// out이 아닌 ref인 이유: 성공 시에는 값을 건드리지 않고 실패 시에만 쓰기 때문이다.
    ///
    /// [caller 파라미터]
    /// 어느 컴포넌트의 어느 메서드에서 예외가 발생했는지 로그에 남기기 위해
    /// CallerMemberName 특성으로 호출한 메서드 이름을 자동으로 수집한다.
    /// </summary>
    /// <param name="action">실행할 비동기 작업</param>
    /// <param name="errorMessage">실패 시 오류 메시지를 저장할 변수 (화면 표시용)</param>
    /// <param name="caller">호출한 메서드 이름 (컴파일러가 자동 삽입)</param>
    protected async Task SafeExecute(
        Func<Task> action,
        Action<string>? onError = null,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
#if DEBUG
        // 디버그 빌드: try-catch 없이 실행.
        // 예외가 발생하면 Blazor의 상세 오류 화면이 그대로 노출되어
        // 개발자가 스택 트레이스를 즉시 확인할 수 있다.
        await action();
#else
        // 릴리즈 빌드: 예외를 잡아 로그에 기록하고 회로를 보호한다.
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            // 컴포넌트 타입명 + 호출 메서드명을 함께 기록하여
            // 로그 파일에서 발생 위치를 빠르게 추적할 수 있도록 한다.
            Logger.LogError(ex, "[Admin 오류] {Component}.{Caller}", GetType().Name, caller);

            // 호출자가 오류 처리 콜백을 넘겼으면 실행한다.
            // (예: errorMessage = "조회에 실패했습니다.")
            onError?.Invoke("오류가 발생했습니다. 잠시 후 다시 시도해주세요.");
        }
#endif
    }
}
