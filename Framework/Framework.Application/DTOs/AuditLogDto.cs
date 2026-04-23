namespace Framework.Application.DTOs;

// 감사 로그 단건 응답 DTO
public record AuditLogDto(
    long Id,
    int PlayerId,
    int ItemId,
    string ItemName,
    string Reason,
    int ChangeAmount,
    int BalanceBefore,
    int BalanceAfter,
    bool IsAnomaly,
    DateTime CreatedAt
);

// 감사 로그 검색 필터 — 쿼리 스트링에서 바인딩
public record AuditLogFilterDto(
    int? PlayerId,
    int? ItemId,
    DateTime? From,
    DateTime? To,
    bool? IsAnomaly,
    int Page = 1,
    int PageSize = 50
);
