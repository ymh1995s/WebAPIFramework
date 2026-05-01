using Framework.Domain.Enums;

namespace Framework.Application.Features.BanLog;

// Admin 밴 이력 응답 DTO — BanLog 엔티티 + 조회 편의용 Nickname
public record BanLogDto(
    long Id,
    int PlayerId,
    string? PlayerNickname,   // Players LEFT JOIN 결과 — 플레이어 삭제 시 null
    BanAction Action,
    DateTime? BannedUntil,
    string? Reason,
    AuditActorType ActorType,
    int? ActorId,
    string? ActorIp,
    DateTime CreatedAt
);

// 페이지네이션 응답 래퍼 — 목록 + 전체 건수 + 페이지 정보
public record BanLogPagedDto(
    List<BanLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
