using Framework.Application.Common;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Quest;

// 퀘스트 Admin 관리 서비스 구현체
// Admin 전용 CRUD — 클라이언트 퀘스트 진행은 QuestProgressService 담당
public class QuestAdminService : IQuestAdminService
{
    private readonly IQuestDefinitionRepository _questDefRepo;
    private readonly IPlayerQuestProgressRepository _progressRepo;
    private readonly IQuestPeriodKeyResolver _periodKeyResolver;
    private readonly ILogger<QuestAdminService> _logger;

    public QuestAdminService(
        IQuestDefinitionRepository questDefRepo,
        IPlayerQuestProgressRepository progressRepo,
        IQuestPeriodKeyResolver periodKeyResolver,
        ILogger<QuestAdminService> logger)
    {
        _questDefRepo = questDefRepo;
        _progressRepo = progressRepo;
        _periodKeyResolver = periodKeyResolver;
        _logger = logger;
    }

    // 퀘스트 정의 목록 조회 — Admin 검색
    public async Task<PagedResultDto<QuestDefinitionDto>> SearchAsync(
        string? keyword, QuestPeriod? period, bool? isActive, int page, int pageSize)
    {
        var (items, total) = await _questDefRepo.SearchAsync(keyword, period, isActive, page, pageSize);
        return new PagedResultDto<QuestDefinitionDto>(
            Items: items.Select(ToDto).ToList(),
            TotalCount: total,
            Page: page,
            PageSize: pageSize
        );
    }

    // 단건 조회
    public async Task<QuestDefinitionDto?> GetByIdAsync(int id)
    {
        var quest = await _questDefRepo.GetByIdAsync(id);
        return quest is null ? null : ToDto(quest);
    }

    // 퀘스트 정의 생성
    public async Task<QuestDefinitionDto> CreateAsync(CreateQuestDefinitionDto dto)
    {
        var quest = new QuestDefinition
        {
            Code = dto.Code.Trim(),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Period = dto.Period,
            ConditionType = dto.ConditionType,
            ConditionTargetId = dto.ConditionTargetId,
            TargetAmount = dto.TargetAmount,
            RewardTableId = dto.RewardTableId,
            PrerequisiteQuestId = dto.PrerequisiteQuestId,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _questDefRepo.AddAsync(quest);
        await _questDefRepo.SaveChangesAsync();

        _logger.LogInformation("퀘스트 정의 생성 — Id={Id}, Code={Code}, Period={Period}",
            quest.Id, quest.Code, quest.Period);
        return ToDto(quest);
    }

    // 퀘스트 정의 수정 (Period/Code 변경 불가 — 기존 진행 데이터 무결성 보장)
    public async Task<bool> UpdateAsync(int id, UpdateQuestDefinitionDto dto)
    {
        var quest = await _questDefRepo.GetByIdAsync(id);
        if (quest is null) return false;

        quest.Title = dto.Title.Trim();
        quest.Description = dto.Description?.Trim();
        quest.ConditionType = dto.ConditionType;
        quest.ConditionTargetId = dto.ConditionTargetId;
        quest.TargetAmount = dto.TargetAmount;
        quest.RewardTableId = dto.RewardTableId;
        quest.PrerequisiteQuestId = dto.PrerequisiteQuestId;
        quest.IsActive = dto.IsActive;
        quest.SortOrder = dto.SortOrder;
        quest.UpdatedAt = DateTimeOffset.UtcNow;

        await _questDefRepo.SaveChangesAsync();

        _logger.LogInformation("퀘스트 정의 수정 — Id={Id}, Code={Code}", quest.Id, quest.Code);
        return true;
    }

    // 퀘스트 정의 소프트 삭제
    public async Task<bool> DeleteAsync(int id)
    {
        var quest = await _questDefRepo.GetByIdAsync(id);
        if (quest is null) return false;

        quest.IsDeleted = true;
        quest.IsActive = false;
        quest.UpdatedAt = DateTimeOffset.UtcNow;

        await _questDefRepo.SaveChangesAsync();

        _logger.LogInformation("퀘스트 정의 소프트 삭제 — Id={Id}, Code={Code}", quest.Id, quest.Code);
        return true;
    }

    // 특정 플레이어의 퀘스트 진행 상태 조회 — Admin 플레이어 상세 화면용
    // 모든 주기(일일/주간/영구) 현재 PeriodKey 기준으로 조합
    public async Task<List<QuestProgressDto>> GetPlayerProgressAsync(int playerId)
    {
        // 모든 주기의 현재 PeriodKey 계산
        var dailyKey = _periodKeyResolver.Resolve(QuestPeriod.Daily);
        var weeklyKey = _periodKeyResolver.Resolve(QuestPeriod.Weekly);
        var permanentKey = _periodKeyResolver.Resolve(QuestPeriod.Permanent);

        // 활성 퀘스트 정의 전체 조회
        var definitions = await _questDefRepo.GetActiveListAsync();

        // 각 주기별 진행 상태 조회
        var dailyProgresses = await _progressRepo.GetByPlayerAndPeriodKeyAsync(playerId, dailyKey);
        var weeklyProgresses = await _progressRepo.GetByPlayerAndPeriodKeyAsync(playerId, weeklyKey);
        var permanentProgresses = await _progressRepo.GetByPlayerAndPeriodKeyAsync(playerId, permanentKey);

        // 전체 진행 상태를 QuestId 기준 딕셔너리로 합산
        var progressMap = dailyProgresses
            .Concat(weeklyProgresses)
            .Concat(permanentProgresses)
            .ToDictionary(p => (p.QuestId, p.PeriodKey));

        return definitions.Select(q =>
        {
            var periodKey = q.Period switch
            {
                QuestPeriod.Daily => dailyKey,
                QuestPeriod.Weekly => weeklyKey,
                _ => permanentKey,
            };

            progressMap.TryGetValue((q.Id, periodKey), out var progress);
            var current = progress?.CurrentAmount ?? 0;

            return new QuestProgressDto(
                QuestId: q.Id,
                Code: q.Code,
                Title: q.Title,
                Description: q.Description,
                Period: q.Period,
                ConditionType: q.ConditionType,
                TargetAmount: q.TargetAmount,
                CurrentAmount: current,
                IsCompleted: current >= q.TargetAmount,
                IsClaimed: progress?.IsClaimed ?? false,
                ResetAt: progress?.ResetAt,
                RewardTableId: q.RewardTableId
            );
        }).ToList();
    }

    // 엔티티 → DTO 변환 헬퍼
    private static QuestDefinitionDto ToDto(QuestDefinition q) => new(
        Id: q.Id,
        Code: q.Code,
        Title: q.Title,
        Description: q.Description,
        Period: q.Period,
        ConditionType: q.ConditionType,
        ConditionTargetId: q.ConditionTargetId,
        TargetAmount: q.TargetAmount,
        RewardTableId: q.RewardTableId,
        PrerequisiteQuestId: q.PrerequisiteQuestId,
        IsActive: q.IsActive,
        SortOrder: q.SortOrder,
        IsDeleted: q.IsDeleted,
        CreatedAt: q.CreatedAt,
        UpdatedAt: q.UpdatedAt
    );
}
