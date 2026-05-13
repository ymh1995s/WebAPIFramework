#if DEBUG

using System.ComponentModel.DataAnnotations;

namespace Framework.Api.Requests;

// 디버그 보상 지급 요청 DTO — 개발/QA 환경 전용, 릴리즈 빌드에서 제외됨
// PlayerId: null이면 JWT 클레임의 자신 PlayerId 사용, 지정하면 대상 플레이어 오버라이드 가능
public record DebugGrantRequest(
    // 보상 대상 플레이어 ID — null 시 JWT 클레임의 PlayerId 사용
    int? PlayerId,

    // 지급할 경험치 (0이면 미지급)
    [Range(0, 100_000_000)] int Exp = 0,

    // 지급할 아이템 목록 (null 또는 빈 리스트이면 아이템 미지급)
    [MaxLength(50)] List<DebugGrantItem>? Items = null,

    // 멱등성 키 접미사 — null/공백이면 UUID 자동 생성, 지정 시 "debug:{SourceKey}" 형식 사용
    [RegularExpression(@"^[a-zA-Z0-9._:\-]+$")]
    [MaxLength(64)] string? SourceKey = null
);

// 디버그 지급용 아이템 단건 DTO
public record DebugGrantItem(
    // 지급할 아이템 ID (1 이상)
    [Range(1, int.MaxValue)] int ItemId,

    // 지급 수량 (1 이상, 최대 100만)
    [Range(1, 1_000_000)] int Quantity
);

#endif
