using Framework.Domain.Entities;

// 플레이어 저장소 인터페이스
public interface IPlayerRepository
{
    // DeviceId로 플레이어 조회
    Task<Player?> GetByDeviceIdAsync(string deviceId);

    // GoogleId로 플레이어 조회
    Task<Player?> GetByGoogleIdAsync(string googleId);

    // Id로 플레이어 조회
    Task<Player?> GetByIdAsync(int id);

    // 전체 플레이어 조회 (일괄 우편 발송 시 사용)
    Task<List<Player>> GetAllAsync();

    // 플레이어 추가
    Task AddAsync(Player player);

    // 플레이어 수정 (LastLoginAt 갱신 등)
    Task UpdateAsync(Player player);

    // 플레이어 밴 처리 (bannedUntil: null이면 영구 밴, 값이 있으면 기간 밴)
    Task BanAsync(int playerId, DateTime? bannedUntil);

    // 플레이어 밴 해제
    Task UnbanAsync(int playerId);

    // 플레이어 삭제 (계정 탈퇴 - 연관 데이터 CASCADE 삭제)
    // [참고] 신규 탈퇴 흐름은 WithdrawAnonymizeAsync 사용 (H-12 SoftDelete + PII 익명화 정책)
    Task DeleteAsync(Player player);

    // 탈퇴 시 PII 익명화 — DeviceId/GoogleId NULL, Nickname 표준화, IsDeleted=true
    // IapPurchase Restrict FK 유지를 위해 Player 행 자체는 보존
    Task WithdrawAnonymizeAsync(Player player);

    // 플레이어 소프트 딜리트 — 계정 병합 시 게스트 계정을 논리 삭제하고 병합 대상 ID 기록
    Task SoftDeleteAsync(Player player, int mergedIntoPlayerId);

    // 소프트 딜리트 포함 전체 플레이어 목록 DB 페이지네이션 조회 (Admin 전용)
    Task<(List<Player> Items, int TotalCount)> GetPagedIncludingDeletedAsync(int page, int pageSize);

    // 소프트 딜리트 포함 키워드 검색 DB 페이지네이션 조회 (Admin 전용)
    Task<(List<Player> Items, int TotalCount)> SearchByKeywordPagedIncludingDeletedAsync(string keyword, int page, int pageSize);

    // 소프트 딜리트된 계정을 포함하여 ID로 조회 (Admin 전용)
    Task<Player?> GetByIdIncludingDeletedAsync(int id);

    // 지정된 플레이어들의 AttendanceCount를 1씩 증가 (배치 처리용 ExecuteUpdate)
    Task IncrementAttendanceCountAsync(IEnumerable<int> playerIds);

    // 변경사항을 DB에 반영 — 호출자(Service)가 명시적으로 호출
    Task SaveChangesAsync();
}
