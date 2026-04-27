namespace Framework.Admin.Logging;

/// <summary>
/// 캡처된 HTTP 요청/응답 한 건을 나타내는 불변 레코드.
/// DelegatingHandler에서 생성되어 IHttpLogStore에 저장된다.
/// </summary>
public record HttpLogEntry(
    // 요청이 발생한 시각
    DateTimeOffset Timestamp,
    // HTTP 메서드 (GET, POST 등)
    string Method,
    // 요청 URL (전체 Uri.ToString())
    string Url,
    // HTTP 응답 상태 코드 (실패 시 0)
    int StatusCode,
    // 요청 ~ 응답까지 소요된 시간(ms)
    long ElapsedMs,
    // 발생한 예외 메시지 (정상 응답 시 null)
    string? ErrorMessage
)
{
    /// <summary>
    /// 상태 코드 기반으로 로그 항목의 색상 CSS 클래스를 반환한다.
    /// </summary>
    public string CssClass => StatusCode switch
    {
        >= 500 => "log-error",    // 서버 오류 — 빨강
        >= 400 => "log-warn",     // 클라이언트 오류 — 노랑
        >= 200 and < 300 => "log-ok", // 성공 — 초록
        0 => "log-error",         // 예외 발생으로 코드 없음 — 빨강
        _ => "log-default"        // 그 외
    };
}
