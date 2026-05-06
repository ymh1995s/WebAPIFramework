using Framework.Application.Features.BanLog;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.AdminPlayer;

// Admin 전용 플레이어 관리 서비스 구현체
// 기존 AdminPlayersController에 있던 Repository 직접 호출 로직을 이곳으로 이동
public class AdminPlayerService : IAdminPlayerService
{
    private readonly IPlayerRepository _playerRepository;

    // 밴/밴해제 감사 이력 서비스 — BanLog를 Player 변경과 단일 트랜잭션으로 기록
    private readonly IBanLogService _banLogService;

    // 인앱결제 구매 이력 저장소 — 하드삭제 전 FK Restrict 해소를 위해 선삭제
    private readonly IIapPurchaseRepository _iapPurchaseRepository;

    // 게임 진행 데이터 정리 서비스 — PlayerItem, Mail, DailyLoginLog 등 삭제
    private readonly IPlayerWithdrawalCleaner _withdrawalCleaner;

    // 트랜잭션 단위 — 하드삭제 시 여러 Repository 변경을 원자적으로 처리
    private readonly IUnitOfWork _unitOfWork;

    // 하드삭제 실행 감사 로깅 — 복구 불가 작업이므로 로그 필수
    private readonly ILogger<AdminPlayerService> _logger;

    public AdminPlayerService(
        IPlayerRepository playerRepository,
        IBanLogService banLogService,
        IIapPurchaseRepository iapPurchaseRepository,
        IPlayerWithdrawalCleaner withdrawalCleaner,
        IUnitOfWork unitOfWork,
        ILogger<AdminPlayerService> logger)
    {
        _playerRepository       = playerRepository;
        _banLogService          = banLogService;
        _iapPurchaseRepository  = iapPurchaseRepository;
        _withdrawalCleaner      = withdrawalCleaner;
        _unitOfWork             = unitOfWork;
        _logger                 = logger;
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
        p.IsEffectivelyBanned,
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

    // 밴 처리 — 실효 밴 상태이면 AlreadyBanned 반환 (토글 강제)
    public async Task<BanOperationResult> BanAsync(int id, DateTime? bannedUntil, string? reason, string? actorIp)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return BanOperationResult.PlayerNotFound;

        // 현재 실효적으로 밴 상태이면 중복 밴 방지 (기간 밴 만료 시에는 재밴 허용)
        if (player.IsEffectivelyBanned) return BanOperationResult.AlreadyBanned;

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
        if (!player.IsEffectivelyBanned) return BanOperationResult.NotBanned;

        await _playerRepository.UnbanAsync(id);

        // BanLog 추가 — SaveChanges는 아래에서 한 번만 호출 (Player + BanLog 단일 트랜잭션)
        await _banLogService.AddAsync(player.Id, BanAction.Unban, bannedUntil: null, reason, actorIp);

        await _playerRepository.SaveChangesAsync();
        return BanOperationResult.Success;
    }

    // 플레이어 DB 직접 삭제 — 소프트 딜리트 미적용 계정 대상 관리자 삭제 (기존 DELETE /{id} 호환)
    public async Task<bool> DeleteAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return false;

        await _playerRepository.DeleteAsync(player);
        await _playerRepository.SaveChangesAsync();
        return true;
    }

    // 플레이어 하드삭제 — 탈퇴 처리(IsDeleted=true)된 계정만 허용
    // 처리 순서:
    //   1. IapPurchase 선삭제 (Restrict FK 해소)
    //   2. 게임 진행 데이터 정리 (PlayerWithdrawalCleaner)
    //   3. Player 엔티티 하드삭제
    // 전체 작업을 단일 트랜잭션으로 감싸 원자성 보장
    public async Task<HardDeleteResult> HardDeleteAsync(int id)
    {
        // GetByIdAsync는 소프트 딜리트 미포함 — IsDeleted=true 계정 조회를 위해 별도 조회 필요
        var player = await _playerRepository.GetByIdIncludingDeletedAsync(id);
        if (player is null) return HardDeleteResult.NotFound;

        // 탈퇴 처리된 계정만 하드삭제 허용
        if (!player.IsDeleted) return HardDeleteResult.NotWithdrawn;

        _logger.LogWarning("플레이어 하드삭제 시작 — PlayerId: {PlayerId}, PublicId: {PublicId}", id, player.PublicId);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // IapPurchase는 IPlayerWithdrawalCleaner 대상에서 제외됨 (전자상거래법 보존 의무)
            // Player FK Restrict 제약이 있으므로 Player 삭제 전 직접 선삭제
            await _iapPurchaseRepository.DeleteAllByPlayerAsync(id);

            // PlayerProfile, PlayerItem, Mail 등 게임 진행 데이터 정리
            // 탈퇴 시 이미 실행됐을 수 있으나 멱등하게 재실행
            await _withdrawalCleaner.PurgeGameDataAsync(id);

            // Player 엔티티 하드삭제
            await _playerRepository.DeleteAsync(player);
            await _playerRepository.SaveChangesAsync();
        });

        _logger.LogWarning("플레이어 하드삭제 완료 — PlayerId: {PlayerId}", id);
        return HardDeleteResult.Success;
    }

    // 특정 플레이어의 인앱결제 건수 조회 — 하드삭제 모달에서 결제 이력 소실 경고에 사용
    public async Task<int> GetIapPurchaseCountAsync(int id)
        => await _iapPurchaseRepository.CountByPlayerAsync(id);
}
