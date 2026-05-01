using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using BanLogEntity = Framework.Domain.Entities.BanLog;  // namespace 충돌 방지 별칭

namespace Framework.Application.Features.BanLog;

// 밴 이력 서비스 구현체
// AddAsync: BanLog 엔티티 생성 후 Repository에 등록 (SaveChanges 없음 — 호출자가 책임)
// SearchAsync: Repository에서 LEFT JOIN으로 Nickname 포함 결과 수신 후 DTO 변환
public class BanLogService : IBanLogService
{
    private readonly IBanLogRepository _banLogRepo;

    public BanLogService(IBanLogRepository banLogRepo)
    {
        _banLogRepo = banLogRepo;
    }

    // BanLog 엔티티 생성 및 변경 추적 등록 — SaveChanges는 AdminPlayerService에서 Player 변경과 함께 처리
    public async Task AddAsync(int playerId, BanAction action, DateTime? bannedUntil, string? reason, string? actorIp)
    {
        var log = new BanLogEntity
        {
            PlayerId    = playerId,
            Action      = action,
            BannedUntil = bannedUntil,
            Reason      = reason,
            ActorType   = AuditActorType.Admin,  // 현재는 Admin 고정
            ActorIp     = actorIp,
            CreatedAt   = DateTime.UtcNow,
        };

        await _banLogRepo.AddAsync(log);
    }

    // 이력 목록 조회 — Repository에서 BanLog + Nickname(LEFT JOIN) 수신 후 BanLogDto로 변환
    public async Task<BanLogPagedDto> SearchAsync(
        int? playerId, BanAction? action, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var (items, totalCount) = await _banLogRepo.SearchAsync(playerId, action, from, to, page, pageSize);

        // 튜플 리스트 → DTO 리스트 변환 (BanLogEntity 별칭 사용)
        var dtos = items.Select(x => new BanLogDto(
            x.Log.Id,
            x.Log.PlayerId,
            x.Nickname,         // Players LEFT JOIN 결과 — 삭제된 플레이어는 null
            x.Log.Action,
            x.Log.BannedUntil,
            x.Log.Reason,
            x.Log.ActorType,
            x.Log.ActorId,
            x.Log.ActorIp,
            x.Log.CreatedAt
        )).ToList();

        return new BanLogPagedDto(dtos, totalCount, page, pageSize);
    }
}
