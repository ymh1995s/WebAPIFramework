using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 퀘스트 정의 마스터 — 일일/주간/메인 퀘스트의 조건과 보상을 정의하는 데이터
public class QuestDefinition
{
    // 퀘스트 정의 고유 ID (PK)
    public int Id { get; set; }

    // 퀘스트 코드 — 클라이언트 참조용 고유 문자열 식별자 (UNIQUE)
    public string Code { get; set; } = "";

    // 퀘스트 제목 — 클라이언트 UI 표시용
    public string Title { get; set; } = "";

    // 퀘스트 설명 — 클라이언트 UI 표시용 (null 허용)
    public string? Description { get; set; }

    // 퀘스트 주기 (일일/주간/영구)
    public QuestPeriod Period { get; set; }

    // 달성 조건 타입 (스테이지 클리어/아이템 사용/상점 구매/로그인)
    public QuestConditionType ConditionType { get; set; }

    // 조건 대상 ID — null이면 해당 타입 전체 대상 (예: StageId, ItemId, ShopProductId)
    public int? ConditionTargetId { get; set; }

    // 목표 달성 수량
    public int TargetAmount { get; set; }

    // 보상 테이블 FK → RewardTables
    public int RewardTableId { get; set; }

    // 선행 퀘스트 ID — 이 퀘스트를 수락하기 전 완료해야 할 퀘스트 (null이면 선행 조건 없음)
    public int? PrerequisiteQuestId { get; set; }

    // 활성화 여부 — false이면 클라이언트에 노출하지 않음
    public bool IsActive { get; set; } = true;

    // 정렬 순서 — 낮을수록 앞에 표시
    public int SortOrder { get; set; }

    // 소프트 삭제 여부
    public bool IsDeleted { get; set; }

    // 생성 일시 (UTC)
    public DateTimeOffset CreatedAt { get; set; }

    // 최종 수정 일시 (UTC)
    public DateTimeOffset UpdatedAt { get; set; }

    // 보상 테이블 네비게이션 프로퍼티
    public RewardTable RewardTable { get; set; } = null!;

    // 선행 퀘스트 네비게이션 프로퍼티 (자기 참조, nullable)
    public QuestDefinition? PrerequisiteQuest { get; set; }
}
