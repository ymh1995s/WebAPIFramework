using Framework.Domain.Enums;

namespace Framework.Application.Features.Quest;

// 클라이언트용 퀘스트 진행 상태 DTO
public record QuestProgressDto(
    int QuestId,
    string Code,
    string Title,
    string? Description,
    QuestPeriod Period,
    QuestConditionType ConditionType,
    int TargetAmount,
    int CurrentAmount,
    bool IsCompleted,
    bool IsClaimed,
    DateTimeOffset? ResetAt,
    int RewardTableId
);

// Admin용 퀘스트 정의 DTO
public record QuestDefinitionDto(
    int Id,
    string Code,
    string Title,
    string? Description,
    QuestPeriod Period,
    QuestConditionType ConditionType,
    int? ConditionTargetId,
    int TargetAmount,
    int RewardTableId,
    int? PrerequisiteQuestId,
    bool IsActive,
    int SortOrder,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

// Admin 퀘스트 생성 요청 DTO
public record CreateQuestDefinitionDto(
    string Code,
    string Title,
    string? Description,
    QuestPeriod Period,
    QuestConditionType ConditionType,
    int? ConditionTargetId,
    int TargetAmount,
    int RewardTableId,
    int? PrerequisiteQuestId,
    bool IsActive,
    int SortOrder
);

// Admin 퀘스트 수정 요청 DTO
public record UpdateQuestDefinitionDto(
    string Title,
    string? Description,
    QuestConditionType ConditionType,
    int? ConditionTargetId,
    int TargetAmount,
    int RewardTableId,
    int? PrerequisiteQuestId,
    bool IsActive,
    int SortOrder
);
