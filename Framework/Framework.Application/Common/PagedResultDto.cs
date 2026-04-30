namespace Framework.Application.Common;

// 페이지네이션 결과 래퍼 — 목록 조회 API의 공통 응답 구조
// Items: 현재 페이지 항목 목록
// TotalCount: 전체 데이터 수 (클라이언트가 총 페이지 수 계산에 사용)
// Page: 현재 페이지 번호 (1부터 시작)
// PageSize: 한 페이지당 항목 수
public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    // 전체 페이지 수 — TotalCount ÷ PageSize 올림 계산
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
