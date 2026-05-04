namespace Framework.Admin.Logging;

// HttpLogCaptureHandler 옵션 — 캡처 제외 경로 화이트리스트
public sealed class HttpLogCaptureOptions
{
    // 캡처 제외 경로 (StartsWith 매칭, OrdinalIgnoreCase) — 폴링 컴포넌트 노이즈 차단용
    public IReadOnlyList<string> ExcludedPaths { get; set; } = Array.Empty<string>();
}
