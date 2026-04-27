namespace Framework.Admin.Logging;

/// <summary>
/// 메모리 내 링 버퍼 방식으로 HTTP 로그를 보관하는 구현체.
/// 최대 200건을 유지하며 초과 시 가장 오래된 항목을 자동 제거한다.
/// Singleton으로 등록하여 모든 컴포넌트가 동일 인스턴스를 공유한다.
/// </summary>
public sealed class InMemoryHttpLogStore : IHttpLogStore
{
    // 동시 쓰기 충돌 방지를 위한 잠금 객체
    private readonly Lock _lock = new();

    // 링 버퍼 역할을 하는 연결 리스트 (앞=최신, 뒤=오래된)
    private readonly LinkedList<HttpLogEntry> _entries = new();

    // 보관할 최대 로그 건수
    private const int MaxCapacity = 200;

    /// <inheritdoc/>
    public event Action<HttpLogEntry>? LogAdded;

    /// <inheritdoc/>
    public void Add(HttpLogEntry entry)
    {
        lock (_lock)
        {
            // 최신 항목을 앞에 추가
            _entries.AddFirst(entry);

            // 용량 초과 시 가장 오래된 항목(마지막) 제거
            if (_entries.Count > MaxCapacity)
                _entries.RemoveLast();
        }

        // 잠금 해제 후 이벤트 발생 — UI 스레드 블로킹 방지
        LogAdded?.Invoke(entry);
    }

    /// <inheritdoc/>
    public IReadOnlyList<HttpLogEntry> GetAll()
    {
        lock (_lock)
        {
            // 스냅샷을 반환하여 이후 변경과 격리
            return [.. _entries];
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
