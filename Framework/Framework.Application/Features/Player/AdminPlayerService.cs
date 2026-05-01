using Framework.Application.Features.BanLog;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.AdminPlayer;

// Admin 전용 플레이어 관리 서비스 구현체
// 기존 AdminPlayersController에 있던 Repository 직접 호출 로직을 이곳으로 이동
public class AdminPlayerService : IAdminPlayerService
{
    private readonly IPlayerRepository _playerRepository;

    // 밴/밴해제 감사 이력 서비스 — BanLog를 Player 변경과 단일 트랜잭션으로 기록
    private readonly IBanLogService _banLogService;

    public AdminPlayerService(IPlayerRepository playerRepository, IBanLogService banLogService)
    {
        _playerRepository = playerRepository;
        _banLogService    = banLogService;
    }

    // Player 엔티티를 AdminPlayerDto로 변환하는 내부 헬퍼
    private static AdminPlayerDto ToDto(Player p) => new(
        p.Id,
        p.PublicId,
        p.DeviceId,
        p.Nickname,
        p.GoogleId,
        p.CreatedAt,
        p.LastLoginAt,
        p.IsBanned,
        p.BannedUntil,
        p.IsDeleted,
        p.DeletedAt,
        p.MergedIntoPlayerId
    );

    // 전체 플레이어 목록 조회 (소프트 딜리트 포함) — DB 레벨 페이지네이션으로 메모리 적재 최소화
    public async Task<AdminPlayerListDto> GetAllAsync(int page, int pageSize)
    {
        var (players, total) = await _playerRepository.GetPagedIncludingDeletedAsync(page, pageSize);
        var items = players.Select(ToDto).ToList();
        return new AdminPlayerListDto(items, total, page, pageSize);
    }

    // 키워드 부분 일치 검색 — DB 레벨 페이지네이션, DeviceId·닉네임 대상, 소프트 딜리트 포함
    public async Task<AdminPlayerListDto> SearchAsync(string keyword, int page, int pageSize)
    {
        var (players, total) = await _playerRepository.SearchByKeywordPagedIncludingDeletedAsync(keyword, page, pageSize);
        var items = players.Select(ToDto).ToList();
        return new AdminPlayerListDto(items, total, page, pageSize);
    }

    // ID로 단건 조회 — 소프트 딜리트된 계정은 포함하지 않음
    public async Task<AdminPlayerDto?> GetByIdAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        return player is null ? null : ToDto(player);
    }

    // 실효 밴 여부 — IsBanned 플래그와 BannedUntil 시각을 모두 고려 (AuthService와 동일 기준)
    private static bool IsEffectivelyBanned(Player p) =>
        p.IsBanned && (p.BannedUntil == null || p.BannedUntil > DateTime.UtcNow);

    // 밴 처리 — 실효 밴 상태이면 AlreadyBanned 반환 (토글 강제)
    public async Task<BanOperationResult> BanAsync(int id, DateTime? bannedUntil, string? reason, string? actorIp)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return BanOperationResult.PlayerNotFound;

        // 현재 실효적으로 밴 상태이면 중복 밴 방지 (기간 밴 만료 시에는 재밴 허용)
        if (IsEffectivelyBanned(player)) return BanOperationResult.AlreadyBanned;

        await _playerRepository.BanAsync(id, bannedUntil);

        // BanLog 추가 — SaveChanges는 아래에서 한 번만 호출 (Player + BanLog 단일 트랜잭션)
        await _banLogService.AddAsync(player.Id, BanAction.Ban, bannedUntil, reason, actorIp);

        await _playerRepository.SaveChangesAsync();
        return BanOperationResult.Success;
    }

    // 밴 해제 — 실효 밴 상태가 아니면 NotBanned 반환 (토글 강제)
    public async Task<BanOperationResult> UnbanAsync(int id, string? reason, string? actorIp)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return BanOperationResult.PlayerNotFound;

        // 실효적으로 밴 상태가 아니면 해제 불가 (만료된 기간 밴은 이미 자동 해제된 것으로 간주)
        if (!IsEffectivelyBanned(player)) return BanOperationResult.NotBanned;

        await _playerRepository.UnbanAsync(id);

        // BanLog 추가 — SaveChanges는 아래에서 한 번만 호출 (Player + BanLog 단일 트랜잭션)
        await _banLogService.AddAsync(player.Id, BanAction.Unban, bannedUntil: null, reason, actorIp);

        await _playerRepository.SaveChangesAsync();
        return BanOperationResult.Success;
    }

    // 영구 삭제 (Hard Delete) — DB에서 완전히 제거, 복구 불가
    public async Task<bool> DeleteAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return false;

        await _playerRepository.DeleteAsync(player);
        await _playerRepository.SaveChangesAsync();
        return true;
    }
}
