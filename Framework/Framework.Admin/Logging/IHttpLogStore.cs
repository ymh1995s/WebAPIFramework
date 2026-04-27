namespace Framework.Admin.Logging;

/// <summary>
/// HTTP 로그 항목의 저장소 인터페이스.
/// 컴포넌트는 이 인터페이스를 통해서만 로그에 접근한다.
/// </summary>
public interface IHttpLogStore
{
    /// <summary>
    /// 새 로그 항목을 저장소에 추가한다.
    /// 최대 용량 초과 시 가장 오래된 항목이 제거된다.
    /// </summary>
    void Add(HttpLogEntry entry);

    /// <summary>
    /// 저장된 모든 로그 항목을 최신순으로 반환한다.
    /// </summary>
    IReadOnlyList<HttpLogEntry> GetAll();

    /// <summary>
    /// 저장소의 모든 로그 항목을 삭제한다.
    /// </summary>
    void Clear();

    /// <summary>
    /// 새 로그 항목이 추가될 때 발생하는 이벤트.
    /// Blazor 컴포넌트에서 구독하여 실시간 UI 갱신에 사용한다.
    /// </summary>
    event Action<HttpLogEntry> LogAdded;
}
