namespace Framework.Application.Features.AdminPlayer;

// Admin 플레이어 목록/단건 조회 응답 DTO
public record AdminPlayerDto(
    int Id,
    Guid PublicId,
    string? DeviceId,
    string Nickname,
    string? GoogleId,
    DateTime CreatedAt,
    DateTime LastLoginAt,
    bool IsBanned,
    DateTime? BannedUntil,
    // 실효 밴 여부 — 만료된 기간 밴은 false, Admin UI 표시 및 버튼 분기에 사용
    bool IsEffectivelyBanned,
    bool IsDeleted,
    DateTime? DeletedAt,
    int? MergedIntoPlayerId
);

// 페이지네이션 목록 응답 DTO
public record AdminPlayerListDto(
    List<AdminPlayerDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// 밴 처리 요청 DTO
public record AdminBanPlayerDto(DateTime? BannedUntil);
