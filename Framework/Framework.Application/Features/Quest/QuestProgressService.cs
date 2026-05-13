using Framework.Application.Common;
using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.Reward;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Quest;

// 플레이어 퀘스트 진행 서비스 구현체
// [캐시 전략] 활성 퀘스트 정의를 IMemoryCache에 5분간 캐시 — IncrementAsync 호출 경로(매 게임 액션)에서
//             DB 조회를 줄여 성능 최적화. 퀘스트 추가/비활성화 시 캐시가 자동 만료됨.
// [ClaimAsync] 1. 완료 여부 확인 → 2. xmin 동시성 보호 트랜잭션(3회 재시도) → 3. 보상 지급(트랜잭션 외부)
// [IncrementAsync] 캐시된 퀘스트 목록 매칭 → UPSERT(없으면 INSERT, 있으면 UPDATE) → 실패 시 LogWarning
public class QuestProgressService : IQuestProgressService
{
    // 활성 퀘스트 정의 캐시 키
    private const string ActiveQuestsCacheKey = "quest:active-definitions";

    // 퀘스트 정의 캐시 유효 시간 — 5분 (퀘스트 추가 후 최대 5분 지연 허용)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IQuestDefinitionRepository _questDefRepo;
    private readonly IPlayerQuestProgressRepository _progressRepo;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IAdminNotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuestPeriodKeyResolver _periodKeyResolver;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QuestProgressService> _logger;

    public QuestProgressService(
        IQuestDefinitionRepository questDefRepo,
        IPlayerQuestProgressRepository progressRepo,
        IRewardTableRepository rewardTableRepo,
        IRewardDispatcher rewardDispatcher,
        IAdminNotificationService notificationService,
        IUnitOfWork unitOfWork,
        IQuestPeriodKeyResolver periodKeyResolver,
        IMemoryCache cache,
        ILogger<QuestProgressService> logger)
    {
        _questDefRepo = questDefRepo;
        _progressRepo = progressRepo;
        _rewardTableRepo = rewardTableRepo;
        _rewardDispatcher = rewardDispatcher;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _periodKeyResolver = periodKeyResolver;
        _cache = cache;
        _logger = logger;
    }

    // 퀘스트 조건 카운터 증가 — 기존 서비스에서 트랜잭션 커밋 후 호출
    // [흐름] 캐시 조회 → 조건 매칭 → PeriodKey 계산 → UPSERT (존재하면 amount 누적, 없으면 신규)
    // [실패 처리] LogWarning만 기록 — 게임 플레이에 영향 없도록 예외를 삼키지 않고 경고만 남김
    public async Task IncrementAsync(int playerId, QuestConditionType conditionType, int amount, int? targetId)
    {
        try
        {
            // 캐시에서 활성 퀘스트 정의 조회 (없으면 DB에서 로드 후 캐시)
            var activeQuests = await GetCachedActiveQuestsAsync();

            // 조건 타입 + 대상 ID 매칭 — (타입 일치) AND (대상 ID null이거나 대상 ID 일치)
            var matchedQuests = activeQuests
                .Where(q => q.ConditionType == conditionType &&
                            (q.ConditionTargetId == null || q.ConditionTargetId == targetId))
                .ToList();

            // 매칭된 퀘스트가 없으면 조기 반환
            if (matchedQuests.Count == 0) return;

            // 매칭된 각 퀘스트마다 카운터 증가 처리
            foreach (var quest in matchedQuests)
            {
                var periodKey = _periodKeyResolver.Resolve(quest.Period);
                await UpsertProgressAsync(playerId, quest, periodKey, amount);
            }
        }
        catch (Exception ex)
        {
            // 퀘스트 카운터 실패는 게임 플레이 차단하지 않음 — 경고 로그만 기록
            _logger.LogWarning(
                ex,
                "퀘스트 카운터 증가 실패 — PlayerId={PlayerId}, ConditionType={Type}, Amount={Amount}, TargetId={TargetId}",
                playerId, conditionType, amount, targetId);
        }
    }

    // 플레이어의 특정 주기 퀘스트 목록 조회 — 퀘스트 정의 + 진행 상태 조합
    public async Task<List<QuestProgressDto>> GetProgressListAsync(int playerId, QuestPeriod period)
    {
        // 해당 주기의 활성 퀘스트 정의 조회
        var definitions = await _questDefRepo.GetActiveByPeriodAsync(period);
        if (definitions.Count == 0) return new List<QuestProgressDto>();

        // 현재 PeriodKey 계산 (영구 퀘스트는 "permanent")
        var periodKey = _periodKeyResolver.Resolve(period);

        // 플레이어의 현재 주기 진행 상태 조회 (PeriodKey 기준)
        var progresses = await _progressRepo.GetByPlayerAndPeriodKeyAsync(playerId, periodKey);
        var progressMap = progresses.ToDictionary(p => p.QuestId);

        // 퀘스트 정의 + 진행 상태 합산 → DTO 변환
        return definitions.Select(q =>
        {
            progressMap.TryGetValue(q.Id, out var progress);
            var current = progress?.CurrentAmount ?? 0;
            var isCompleted = current >= q.TargetAmount;
            var isClaimed = progress?.IsClaimed ?? false;

            return new QuestProgressDto(
                QuestId: q.Id,
                Code: q.Code,
                Title: q.Title,
                Description: q.Description,
                Period: q.Period,
                ConditionType: q.ConditionType,
                TargetAmount: q.TargetAmount,
                CurrentAmount: current,
                IsCompleted: isCompleted,
                IsClaimed: isClaimed,
                ResetAt: progress?.ResetAt,
                RewardTableId: q.RewardTableId
            );
        }).ToList();
    }

    // 퀘스트 보상 수령 처리 — xmin 동시성 보호 (3회 재시도) + 보상 지급(트랜잭션 외부)
    // 반환값: "success" | "alreadyClaimed" | "notEligible" | "notFound"
    public async Task<string> ClaimAsync(int playerId, int questId)
    {
        // 1단계: 퀘스트 정의 조회 (없거나 비활성/삭제 → 404)
        var quest = await _questDefRepo.GetByIdAsync(questId);
        if (quest is null || quest.IsDeleted || !quest.IsActive)
        {
            _logger.LogWarning("퀘스트 보상 수령 실패 — 퀘스트 없음/비활성: PlayerId={PlayerId}, QuestId={QuestId}",
                playerId, questId);
            return "notFound";
        }

        // 2단계: PeriodKey 계산
        var periodKey = _periodKeyResolver.Resolve(quest.Period);

        // 3단계: 진행 상태 조회
        var progress = await _progressRepo.FindAsync(playerId, questId, periodKey);

        // 달성량 미충족 → 400 NotEligible
        if (progress is null || progress.CurrentAmount < quest.TargetAmount)
        {
            _logger.LogInformation("퀘스트 보상 수령 불가 — 달성 미완료: PlayerId={PlayerId}, QuestId={QuestId}, Current={Current}, Target={Target}",
                playerId, questId, progress?.CurrentAmount ?? 0, quest.TargetAmount);
            return "notEligible";
        }

        // 이미 수령 → 200 AlreadyClaimed (멱등)
        if (progress.IsClaimed)
        {
            return "alreadyClaimed";
        }

        // 4단계: 트랜잭션 내 IsClaimed 전환 (xmin 동시성, 3회 재시도)
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // xmin 보호를 위해 트랜잭션 내 최신 상태 재조회
                    var freshProgress = await _progressRepo.FindAsync(playerId, questId, periodKey);
                    if (freshProgress is null || freshProgress.IsClaimed) return;

                    freshProgress.IsClaimed = true;
                    freshProgress.ClaimedAt = DateTimeOffset.UtcNow;
                    freshProgress.UpdatedAt = DateTimeOffset.UtcNow;
                    await _progressRepo.SaveChangesAsync();
                });
                break; // 성공 시 재시도 루프 탈출
            }
            catch (DbUpdateConcurrencyException)
            {
                // xmin 충돌 — 마지막 시도에서도 실패 시 예외 전파
                if (attempt >= maxAttempts)
                {
                    _logger.LogWarning("퀘스트 ClaimAsync xmin 충돌 한도 초과 — PlayerId={PlayerId}, QuestId={QuestId}",
                        playerId, questId);
                    throw;
                }
                _unitOfWork.ClearChangeTracker();
            }
        }

        // 5단계: 트랜잭션 외부 — 보상 번들 구성 + 지급 (IRewardDispatcher.GrantAsync)
        var sourceKey = SourceKeys.Quest(questId, periodKey);

        // 보상 테이블에서 번들 구성 — 항목이 없으면 운영자 알림 후 조기 반환
        var rewardTable = await _rewardTableRepo.GetByIdWithEntriesAsync(quest.RewardTableId);
        if (rewardTable is null || rewardTable.Entries.Count == 0)
        {
            _logger.LogError(
                "퀘스트 보상 테이블 비어있음 — QuestId={QuestId}, RewardTableId={TableId}",
                questId, quest.RewardTableId);

            await _notificationService.CreateAsync(
                category: AdminNotificationCategory.RewardDispatchFailure,
                severity: AdminNotificationSeverity.Critical,
                title: "퀘스트 보상 테이블 비어있음",
                message: $"퀘스트 보상 테이블 비어있음 — QuestId={questId}, RewardTableId={quest.RewardTableId}",
                relatedEntityType: "QuestDefinition",
                relatedEntityId: questId,
                dedupKey: $"quest-empty-table:{questId}:{quest.RewardTableId}");

            return "success"; // IsClaimed는 이미 true로 전환됨 — 클라이언트에는 성공 응답
        }

        var bundle = new RewardBundle(Items: rewardTable.Entries
            .Select(e => new RewardItem(e.ItemId, e.Count))
            .ToList());

        var grantRequest = new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.QuestComplete,
            SourceKey: sourceKey,
            Bundle: bundle,
            MailTitle: $"퀘스트 완료 보상: {quest.Title}",
            MailBody: $"'{quest.Title}' 퀘스트 완료 보상입니다.",
            Mode: DispatchMode.Auto
        );

        try
        {
            var result = await _rewardDispatcher.GrantAsync(grantRequest);

            if (!result.Success && !result.AlreadyGranted)
            {
                // 보상 지급 실패 — 운영자 알림 등록 (수동 지급 필요)
                _logger.LogError(
                    "퀘스트 보상 지급 실패 — PlayerId={PlayerId}, QuestId={QuestId}, PeriodKey={Key}, Message={Msg}",
                    playerId, questId, periodKey, result.Message);

                await _notificationService.CreateAsync(
                    category: AdminNotificationCategory.RewardDispatchFailure,
                    severity: AdminNotificationSeverity.Critical,
                    title: "퀘스트 보상 누락",
                    message: $"퀘스트 보상 누락 — PlayerId={playerId}, QuestId={questId}, PeriodKey={periodKey}",
                    relatedEntityType: "PlayerQuestProgress",
                    relatedEntityId: playerId,
                    dedupKey: $"quest-reward-fail:{playerId}:{questId}:{periodKey}");
            }
        }
        catch (Exception ex)
        {
            // 보상 지급 중 예외 — 운영자 알림 등록
            _logger.LogError(
                ex,
                "퀘스트 보상 지급 중 예외 — PlayerId={PlayerId}, QuestId={QuestId}, PeriodKey={Key}",
                playerId, questId, periodKey);

            await _notificationService.CreateAsync(
                category: AdminNotificationCategory.RewardDispatchFailure,
                severity: AdminNotificationSeverity.Critical,
                title: "퀘스트 보상 누락",
                message: $"퀘스트 보상 누락 — PlayerId={playerId}, QuestId={questId}, PeriodKey={periodKey}",
                relatedEntityType: "PlayerQuestProgress",
                relatedEntityId: playerId,
                dedupKey: $"quest-reward-fail:{playerId}:{questId}:{periodKey}");
        }

        return "success";
    }

    // UPSERT: 진행 상태가 없으면 INSERT, 있으면 CurrentAmount 누적 (TargetAmount 상한 적용)
    private async Task UpsertProgressAsync(int playerId, Domain.Entities.QuestDefinition quest, string periodKey, int amount)
    {
        var existing = await _progressRepo.FindAsync(playerId, quest.Id, periodKey);

        if (existing is null)
        {
            // 신규 진행 상태 생성 — TargetAmount 상한 적용
            var newProgress = new PlayerQuestProgress
            {
                PlayerId = playerId,
                QuestId = quest.Id,
                PeriodKey = periodKey,
                CurrentAmount = Math.Min(amount, quest.TargetAmount),
                IsClaimed = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _progressRepo.AddAsync(newProgress);
        }
        else
        {
            // 이미 완료된 퀘스트 (IsClaimed 여부 무관) — TargetAmount를 초과하지 않도록 cap 적용
            if (existing.CurrentAmount >= quest.TargetAmount) return;

            existing.CurrentAmount = Math.Min(existing.CurrentAmount + amount, quest.TargetAmount);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _progressRepo.SaveChangesAsync();
    }

    // IMemoryCache에서 활성 퀘스트 정의 조회 (없으면 DB 로드 후 캐시)
    private async Task<List<Domain.Entities.QuestDefinition>> GetCachedActiveQuestsAsync()
    {
        if (_cache.TryGetValue(ActiveQuestsCacheKey, out List<Domain.Entities.QuestDefinition>? cached) && cached is not null)
            return cached;

        // 캐시 미스 — DB에서 로드
        var quests = await _questDefRepo.GetActiveListAsync();
        _cache.Set(ActiveQuestsCacheKey, quests, CacheDuration);
        return quests;
    }
}
