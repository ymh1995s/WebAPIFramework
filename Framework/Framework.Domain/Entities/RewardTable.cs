using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 보상 테이블 마스터 — 보상 원천별 보상 구성 정의
// UNIQUE(SourceType, Code)로 동일 원천에 같은 코드 중복 방지
public class RewardTable
{
    // 기본 키
    public int Id { get; set; }

    // 보상 적용 원천 타입
    public RewardSourceType SourceType { get; set; }

    // 보상 테이블 식별 코드 (예: "day1", "tier_gold", "level10")
    public string Code { get; set; } = string.Empty;

    // 보상 테이블 설명 (Admin 표시용)
    public string Description { get; set; } = string.Empty;

    // 소프트 삭제 여부 — true이면 논리 삭제 (실제 DB 행은 유지)
    public bool IsDeleted { get; set; } = false;

    // 보상 항목 목록 (1보상 = N행)
    public ICollection<RewardTableEntry> Entries { get; set; } = new List<RewardTableEntry>();
}
