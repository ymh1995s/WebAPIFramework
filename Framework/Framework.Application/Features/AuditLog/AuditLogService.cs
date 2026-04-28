using Framework.Application.Common;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.AuditLog;

// 감사 로그 서비스 구현체
// 호출 시점: 인벤토리/재화 변동 직후, 변경 전/후 수량을 모두 전달
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IItemRepository _itemRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository, IItemRepository itemRepository)
    {
        _auditLogRepository = auditLogRepository;
        _itemRepository = itemRepository;
    }

    // 변동 기록 — Item.AuditLevel 값에 따라 저장 여부/이상치 여부 결정
    public async Task RecordAsync(int playerId, int itemId, string reason, int changeAmount, int balanceBefore, int balanceAfter)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item is null) return;

        // 이상치 여부 — AnomalyThreshold > 0 이고 변동 절대값이 이를 초과하면 true
        var isAnomaly = item.AnomalyThreshold > 0 && Math.Abs(changeAmount) > item.AnomalyThreshold;

        // AnomalyOnly인데 이상치가 아니면 기록 생략 — 로그 볼륨 최소화
        if (item.AuditLevel == AuditLevel.AnomalyOnly && !isAnomaly)
            return;

        var log = new Domain.Entities.AuditLog
        {
            PlayerId = playerId,
            ItemId = itemId,
            Reason = reason,
            ChangeAmount = changeAmount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            IsAnomaly = isAnomaly,
            CreatedAt = DateTime.UtcNow
        };
        await _auditLogRepository.AddAsync(log);
        await _auditLogRepository.SaveChangesAsync();
    }

    // Admin 조회 — 필터/페이지네이션 적용, 아이템 이름 조인
    public async Task<PagedResultDto<AuditLogDto>> SearchAsync(AuditLogFilterDto filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 50 : filter.PageSize;

        var (items, total) = await _auditLogRepository.SearchAsync(
            filter.PlayerId, filter.ItemId, filter.From, filter.To, filter.IsAnomaly, page, pageSize);

        // 아이템 이름 조회용 캐시 (결과 내 중복 ItemId 대응)
        var itemIds = items.Select(l => l.ItemId).Distinct().ToList();
        var itemNameMap = new Dictionary<int, string>();
        foreach (var id in itemIds)
        {
            var item = await _itemRepository.GetByIdAsync(id);
            itemNameMap[id] = item?.Name ?? "(삭제된 아이템)";
        }

        var dtos = items.Select(l => new AuditLogDto(
            l.Id, l.PlayerId, l.ItemId,
            itemNameMap.TryGetValue(l.ItemId, out var name) ? name : "",
            l.Reason, l.ChangeAmount, l.BalanceBefore, l.BalanceAfter,
            l.IsAnomaly, l.CreatedAt
        )).ToList();

        return new PagedResultDto<AuditLogDto>(dtos, total, page, pageSize);
    }
}
