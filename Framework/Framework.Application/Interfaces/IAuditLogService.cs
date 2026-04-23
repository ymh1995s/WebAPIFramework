using Framework.Application.DTOs;

namespace Framework.Application.Interfaces;

// 감사 로그 서비스 인터페이스
public interface IAuditLogService
{
    // 변동 발생 지점에서 호출 — Item.AuditLevel에 따라 저장 여부가 결정됨
    Task RecordAsync(int playerId, int itemId, string reason, int changeAmount, int balanceBefore, int balanceAfter);

    // Admin 조회용 — 페이지네이션 포함 검색
    Task<PagedResultDto<AuditLogDto>> SearchAsync(AuditLogFilterDto filter);
}
