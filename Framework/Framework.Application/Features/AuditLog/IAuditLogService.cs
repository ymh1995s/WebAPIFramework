using Framework.Application.Common;
using Framework.Domain.Enums;

namespace Framework.Application.Features.AuditLog;

// 감사 로그 서비스 인터페이스
public interface IAuditLogService
{
    // 변동 발생 지점에서 호출 — Item.AuditLevel에 따라 저장 여부가 결정됨
    // actorType/actorId: 기본값 Player(0)/null — 기존 호출부는 수정 없이 그대로 사용 가능
    Task RecordAsync(int playerId, int itemId, string reason, int changeAmount, int balanceBefore, int balanceAfter,
        AuditActorType actorType = AuditActorType.Player, int? actorId = null);

    // Admin 조회용 — 페이지네이션 포함 검색
    Task<PagedResultDto<AuditLogDto>> SearchAsync(AuditLogFilterDto filter);
}
