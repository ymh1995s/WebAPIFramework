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

    // 플레이어 밴 처리 (bannedUntil: null이면 영구 밴) — 이미 밴 상태면 AlreadyBanned 반환
    Task<BanOperationResult> BanAsync(int id, DateTime? bannedUntil, string? reason, string? actorIp);

    // 플레이어 밴 해제 — 밴 상태가 아니면 NotBanned 반환
    Task<BanOperationResult> UnbanAsync(int id, string? reason, string? actorIp);

    // 플레이어 DB 직접 삭제 — 소프트 딜리트 미적용 계정 대상 관리자 삭제 (기존 DELETE /{id} 유지)
    Task<bool> DeleteAsync(int id);

    // 플레이어 영구 삭제 (Hard Delete) — 소프트 딜리트 상태인 계정만 허용, IapPurchase 선삭제 포함
    Task<HardDeleteResult> HardDeleteAsync(int id);

    // 특정 플레이어의 인앱결제 건수 조회 — 하드삭제 확인 모달 경고 표시용
    Task<int> GetIapPurchaseCountAsync(int id);
}
