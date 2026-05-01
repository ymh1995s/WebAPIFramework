using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 밴/밴해제 이력 저장소 인터페이스
public interface IBanLogRepository
{
    // BanLog 엔티티를 컨텍스트에 추가 — SaveChanges는 호출자 책임
    Task AddAsync(BanLog banLog);

    // 필터/페이지네이션 기반 검색 — Admin 전용 이력 조회
    // Players 테이블과 LEFT JOIN하여 Nickname을 함께 반환 (Infrastructure 레이어에서 구현)
    Task<(List<(BanLog Log, string? Nickname)> Items, int TotalCount)> SearchAsync(
        int? playerId, BanAction? action, DateTime? from, DateTime? to, int page, int pageSize);
}
