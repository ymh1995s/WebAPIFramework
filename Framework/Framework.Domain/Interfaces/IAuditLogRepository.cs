using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 감사 로그 저장소 인터페이스
public interface IAuditLogRepository
{
    // 신규 감사 로그 추가
    Task AddAsync(AuditLog log);

    // 조건별 페이지네이션 조회 (Admin 용도)
    // 전달된 필터 조건 중 null이 아닌 것만 AND로 결합
    Task<(List<AuditLog> items, int totalCount)> SearchAsync(
        int? playerId,
        int? itemId,
        DateTime? from,
        DateTime? to,
        bool? isAnomaly,
        int page,
        int pageSize);

    Task SaveChangesAsync();
}
