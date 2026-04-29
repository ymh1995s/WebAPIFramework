using Framework.Domain.Enums;

namespace Framework.Application.Features.RewardGrant;

// 보상 지급 이력 목록용 DTO
public record RewardGrantDto(
    int Id,
    int PlayerId,
    RewardSourceType SourceType,
    string SourceKey,
    DateTime GrantedAt,
    // 지급 방식: MailId가 있으면 Mail, 없으면 Direct
    bool IsMailGrant,
    int? MailId
);

// 보상 지급 이력 상세 DTO (BundleSnapshot 포함)
public record RewardGrantDetailDto(
    int Id,
    int PlayerId,
    RewardSourceType SourceType,
    string SourceKey,
    DateTime GrantedAt,
    bool IsMailGrant,
    int? MailId,
    // 지급 당시 번들 스냅샷 (JSON 문자열)
    string BundleSnapshot
);

// 보상 지급 이력 검색 필터 DTO
public record RewardGrantFilterDto(
    int? PlayerId = null,
    RewardSourceType? SourceType = null,
    string? SourceKey = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50
);
