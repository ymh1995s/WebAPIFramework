namespace Framework.Application.Features.AdminPlayer;

// Admin 전용 플레이어 관리 서비스 인터페이스
public interface IAdminPlayerService
{
    // 전체 플레이어 목록 조회 (소프트 딜리트 포함, 페이지네이션)
    Task<AdminPlayerListDto> GetAllAsync(int page, int pageSize);

    // 키워드로 플레이어 검색 (소프트 딜리트 포함, 페이지네이션)
    Task<AdminPlayerListDto> SearchAsync(string keyword, int page, int pageSize);

    // ID로 플레이어 단건 조회 (소프트 딜리트 미포함)
    Task<AdminPlayerDto?> GetByIdAsync(int id);

    // 플레이어 밴 처리 (bannedUntil: null이면 영구 밴)
    Task<bool> BanAsync(int id, DateTime? bannedUntil);

    // 플레이어 밴 해제
    Task<bool> UnbanAsync(int id);

    // 플레이어 영구 삭제 (Hard Delete)
    Task<bool> DeleteAsync(int id);
}
