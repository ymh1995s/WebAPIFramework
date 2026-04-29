using Framework.Domain.Enums;

namespace Framework.Application.Features.AdPolicy;

// 광고 정책 목록/단건 조회용 DTO
public record AdPolicyDto(
    int Id,
    AdNetworkType Network,
    string PlacementId,
    AdPlacementType PlacementType,
    int? RewardTableId,
    int DailyLimit,
    bool IsEnabled,
    string Description,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// 광고 정책 생성 요청 DTO
public record CreateAdPolicyDto(
    // 광고 네트워크 종류
    AdNetworkType Network,

    // 광고 게재 위치 식별자 (네트워크에서 관리하는 ID)
    string PlacementId,

    // 광고 게재 위치 타입
    AdPlacementType PlacementType,

    // 연결할 보상 테이블 ID (null이면 보상 없음)
    int? RewardTableId,

    // 하루 최대 지급 횟수 (0이면 무제한)
    int DailyLimit,

    // 정책 활성화 여부
    bool IsEnabled,

    // 정책 설명
    string Description
);

// 광고 정책 수정 요청 DTO — PlacementId/Network는 불변
public record UpdateAdPolicyDto(
    // 연결할 보상 테이블 ID (null이면 보상 없음)
    int? RewardTableId,

    // 하루 최대 지급 횟수 (0이면 무제한)
    int DailyLimit,

    // 정책 활성화 여부
    bool IsEnabled,

    // 정책 설명
    string Description
);

// 광고 정책 검색 필터 DTO
public record AdPolicyFilterDto(
    // 광고 네트워크 필터 (null이면 전체)
    AdNetworkType? Network = null,
    int Page = 1,
    int PageSize = 20
);
