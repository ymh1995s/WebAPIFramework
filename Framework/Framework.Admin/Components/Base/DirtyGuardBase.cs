using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Framework.Admin.Components.Base;

/// <summary>
/// 페이지 이탈 시 저장하지 않은 변경사항 경고를 제공하는 재사용 베이스 클래스.
///
/// [사용법]
/// @inherits DirtyGuardBase
/// - 편집 발생 시: await MarkDirtyAsync()
/// - 저장 완료 후: await MarkCleanAsync()
///
/// [보호 범위]
/// 1. Blazor 내부 네비게이션 — RegisterLocationChangingHandler로 가로채어 confirm 다이얼로그 표시
/// 2. 브라우저 탭 닫기 / 새로고침 / 주소 직접 입력 — beforeunload 이벤트로 처리
/// </summary>
public abstract class DirtyGuardBase : SafeComponentBase, IAsyncDisposable
{
    // Blazor 네비게이션 매니저 (내부 페이지 이동 감지용)
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    // JS 런타임 (beforeunload 이벤트 등록 및 confirm 다이얼로그용)
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    // Blazor 내부 네비게이션 핸들러 해제를 위한 참조
    private IDisposable? _locationChangingHandler;

    /// <summary>변경 여부 상태 — 하위 클래스에서 읽기 가능</summary>
    protected bool IsDirty { get; private set; }

    // Blazor 내부 네비게이션 가드 등록 (컴포넌트 초기화 시점)
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _locationChangingHandler = NavigationManager.RegisterLocationChangingHandler(OnLocationChangingAsync);
    }

    // 첫 렌더링 시 브라우저 beforeunload 이벤트 초기화 (비활성 상태로 시작)
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
            await JS.InvokeVoidAsync("dirtyGuard.setDirty", false);
    }

    // Blazor 내부 페이지 이동 시 변경사항 확인
    // IsDirty 상태일 때만 confirm 다이얼로그를 표시하여 이동을 차단할 수 있음
    private async ValueTask OnLocationChangingAsync(LocationChangingContext context)
    {
        if (!IsDirty) return;

        // 브라우저 기본 confirm 다이얼로그로 사용자 의사 확인
        var confirmed = await JS.InvokeAsync<bool>("confirm", new object[] { "저장하지 않은 변경사항이 있습니다. 페이지를 나가시겠습니까?" });
        if (!confirmed)
            context.PreventNavigation(); // 이동 취소
    }

    /// <summary>
    /// 변경 상태로 표시.
    /// 브라우저 beforeunload 이벤트도 활성화하여 탭 닫기/새로고침도 보호.
    /// </summary>
    protected async Task MarkDirtyAsync()
    {
        IsDirty = true;
        await JS.InvokeVoidAsync("dirtyGuard.setDirty", true);
    }

    /// <summary>
    /// 깨끗한 상태로 초기화.
    /// 저장 완료 후 호출하여 이탈 경고를 비활성화.
    /// </summary>
    protected async Task MarkCleanAsync()
    {
        IsDirty = false;
        await JS.InvokeVoidAsync("dirtyGuard.setDirty", false);
    }

    // 컴포넌트 해제 시 핸들러 정리 및 beforeunload 이벤트 해제
    public virtual async ValueTask DisposeAsync()
    {
        _locationChangingHandler?.Dispose();
        try
        {
            // 컴포넌트가 해제될 때 beforeunload 리스너도 제거
            await JS.InvokeVoidAsync("dirtyGuard.setDirty", false);
        }
        catch
        {
            // 회로(circuit)가 이미 끊긴 경우 JS 호출 실패 가능 — 무시
        }
    }
}
