namespace Framework.Application.Common;

// 페이지네이션 결과 래퍼
public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
