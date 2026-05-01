using Framework.Domain.Enums;

namespace Framework.Application.Features.BanLog;

// 밴 이력 서비스 인터페이스
public interface IBanLogService
{
    // BanLog 추가 — SaveChanges는 호출자(AdminPlayerService) 책임
    // AdminPlayerService가 Player 변경과 단일 SaveChangesAsync로 커밋 (단일 트랜잭션 보장)
    Task AddAsync(int playerId, BanAction action, DateTime? bannedUntil, string? reason, string? actorIp);

    // 필터 기반 이력 조회 (Admin 전용)
    Task<BanLogPagedDto> SearchAsync(int? playerId, BanAction? action, DateTime? from, DateTime? to, int page, int pageSize);
}
