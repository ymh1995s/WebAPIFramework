using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.AdminPlayer;

// Admin 전용 플레이어 관리 서비스 구현체
// 기존 AdminPlayersController에 있던 Repository 직접 호출 로직을 이곳으로 이동
public class AdminPlayerService : IAdminPlayerService
{
    private readonly IPlayerRepository _playerRepository;

    public AdminPlayerService(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
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

    // 전체 플레이어 목록 조회 (소프트 딜리트 포함, 생성일 내림차순 정렬 후 페이지네이션)
    public async Task<AdminPlayerListDto> GetAllAsync(int page, int pageSize)
    {
        var all = await _playerRepository.GetAllIncludingDeletedAsync();
        var total = all.Count;
        var items = all
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToList();

        return new AdminPlayerListDto(items, total, page, pageSize);
    }

    // 키워드 부분 일치 검색 — DeviceId 또는 닉네임 대상, 소프트 딜리트 포함
    public async Task<List<AdminPlayerDto>> SearchAsync(string keyword)
    {
        var players = await _playerRepository.SearchByKeywordIncludingDeletedAsync(keyword);
        return players
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    // ID로 단건 조회 — 소프트 딜리트된 계정은 포함하지 않음
    public async Task<AdminPlayerDto?> GetByIdAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        return player is null ? null : ToDto(player);
    }

    // 밴 처리 — 플레이어 존재 확인 후 Repository 위임, 존재하지 않으면 false 반환
    public async Task<bool> BanAsync(int id, DateTime? bannedUntil)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return false;

        await _playerRepository.BanAsync(id, bannedUntil);
        await _playerRepository.SaveChangesAsync();
        return true;
    }

    // 밴 해제 — 플레이어 존재 확인 후 Repository 위임, 존재하지 않으면 false 반환
    public async Task<bool> UnbanAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return false;

        await _playerRepository.UnbanAsync(id);
        await _playerRepository.SaveChangesAsync();
        return true;
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
