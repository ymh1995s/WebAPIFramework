using Framework.Domain.Enums;

namespace Framework.Application.Features.RewardTable;

// 보상 테이블 항목 DTO
public record RewardTableEntryDto(
    int Id,
    int ItemId,
    string ItemName,
    int Count,
    int? Weight
);

// 보상 테이블 목록용 DTO (Entries 요약)
public record RewardTableDto(
    int Id,
    RewardSourceType SourceType,
    string Code,
    string Description,
    bool IsDeleted,
    int EntryCount
);

// 보상 테이블 상세 DTO (Entries 포함)
public record RewardTableDetailDto(
    int Id,
    RewardSourceType SourceType,
    string Code,
    string Description,
    bool IsDeleted,
    List<RewardTableEntryDto> Entries
);

// 보상 테이블 생성 요청 DTO
public record CreateRewardTableDto(
    RewardSourceType SourceType,
    string Code,
    string Description
);

// 보상 테이블 수정 요청 DTO (Description만 변경 가능 — SourceType/Code 불변)
public record UpdateRewardTableDto(
    string Description
);

// Entries 일괄 교체 요청 DTO — 단일 항목
public record EntryUpsertDto(
    int ItemId,
    int Count,
    int? Weight
);

// 보상 테이블 검색 필터 DTO
public record RewardTableFilterDto(
    RewardSourceType? SourceType = null,
    string? Code = null,
    int Page = 1,
    int PageSize = 20
);
