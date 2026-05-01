// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Application.Features.Exp;
using Framework.Application.Features.Reward;
using Framework.Domain.Content.Entities;
using Framework.Domain.Content.Interfaces;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Content.Stage;

// 스테이지 클리어 서비스 구현체
// [처리 흐름] 스테이지 검증 → 선행 조건 확인 → 기록 upsert → 최초/재클리어 보상 지급 → Exp 지급
public class StageClearService : IStageClearService
{
    private readonly IStageRepository _stageRepo;
    private readonly IStageClearRepository _clearRepo;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly IExpService _expService;
    private readonly ILogger<StageClearService> _logger;

    public StageClearService(
        IStageRepository stageRepo,
        IStageClearRepository clearRepo,
        IRewardDispatcher rewardDispatcher,
        IRewardTableRepository rewardTableRepo,
        IExpService expService,
        ILogger<StageClearService> logger)
    {
        _stageRepo = stageRepo;
        _clearRepo = clearRepo;
        _rewardDispatcher = rewardDispatcher;
        _rewardTableRepo = rewardTableRepo;
        _expService = expService;
        _logger = logger;
    }

    // 스테이지 클리어 완료 처리
    // [순서]
    // 1. 스테이지 마스터 조회 (없거나 비활성이면 KeyNotFoundException)
    // 2. 선행 스테이지 클리어 여부 확인 (미클리어 시 InvalidOperationException)
    // 3. StageClear 기록 upsert (최초 클리어 or 재클리어 판단)
    // 4. 최초 클리어 보상 지급
    // 5. 재클리어 보상 지급 (감소율 적용)
    // 6. 경험치 지급 (ExpService 위임)
    public async Task<StageClearResponseDto> CompleteAsync(int playerId, int stageId, StageClearRequestDto request)
    {
        // 1단계: 스테이지 마스터 조회
        var stage = await _stageRepo.GetByIdAsync(stageId);
        if (stage is null || !stage.IsActive)
        {
            _logger.LogWarning(
                "스테이지 클리어 실패 — 스테이지 없음/비활성 PlayerId: {PlayerId}, StageId: {StageId}",
                playerId, stageId);
            throw new KeyNotFoundException($"스테이지를 찾을 수 없습니다. StageId: {stageId}");
        }

        // 2단계: 순차 진행 조건 확인
        if (stage.RequiredPrevStageId.HasValue)
        {
            var prevClear = await _clearRepo.FindAsync(playerId, stage.RequiredPrevStageId.Value);
            if (prevClear is null)
            {
                _logger.LogWarning(
                    "스테이지 클리어 실패 — 선행 스테이지 미클리어 PlayerId: {PlayerId}, RequiredPrevStageId: {PrevId}",
                    playerId, stage.RequiredPrevStageId.Value);
                throw new InvalidOperationException(
                    $"선행 스테이지를 먼저 클리어해야 합니다. RequiredPrevStageId: {stage.RequiredPrevStageId.Value}");
            }
        }

        // 3단계: 클리어 기록 upsert
        var existing = await _clearRepo.FindAsync(playerId, stageId);
        bool isFirstClear;
        int clearCount;

        if (existing is null)
        {
            // 최초 클리어 — 신규 레코드 생성
            isFirstClear = true;
            clearCount = 1;
            var newClear = new StageClear
            {
                PlayerId = playerId,
                StageId = stageId,
                FirstClearedAt = DateTime.UtcNow,
                LastClearedAt = DateTime.UtcNow,
                ClearCount = 1,
                BestScore = request.Score,
                BestStars = request.Stars,
                BestClearTimeMs = request.ClearTimeMs
            };
            await _clearRepo.AddAsync(newClear);
        }
        else
        {
            // 재클리어 — 기록 갱신
            isFirstClear = false;
            existing.ClearCount++;
            clearCount = existing.ClearCount;
            existing.LastClearedAt = DateTime.UtcNow;

            // 최고 기록 갱신
            if (request.Score > existing.BestScore) existing.BestScore = request.Score;
            if (request.Stars > existing.BestStars) existing.BestStars = request.Stars;
            if (request.ClearTimeMs > 0 &&
                (existing.BestClearTimeMs == 0 || request.ClearTimeMs < existing.BestClearTimeMs))
                existing.BestClearTimeMs = request.ClearTimeMs;
        }

        await _clearRepo.SaveChangesAsync();

        // 4단계: 보상 지급 처리
        string? firstRewardMessage = null;
        string? replayRewardMessage = null;

        if (isFirstClear && !string.IsNullOrEmpty(stage.RewardTableCode))
        {
            // 최초 클리어 보상 — SourceKey: "stage:{stageId}:first"
            firstRewardMessage = await GrantStageRewardAsync(
                playerId,
                stage.RewardTableCode,
                $"stage:{stageId}:first",
                $"스테이지 {stage.Name} 최초 클리어 보상",
                "첫 번째 클리어를 축하합니다! 보상을 수령하세요.");
        }
        else if (!isFirstClear && !string.IsNullOrEmpty(stage.RePlayRewardTableCode))
        {
            // 재클리어 보상 — 감소율 적용 후 지급 (SourceKey: "stage:{stageId}:replay:{clearCount}")
            replayRewardMessage = await GrantReplayRewardAsync(
                playerId,
                stage,
                clearCount,
                $"stage:{stageId}:replay:{clearCount}");
        }

        // 5단계: 경험치 지급 (ExpService 위임)
        if (stage.ExpReward > 0)
        {
            await _expService.AddExpAsync(playerId, stage.ExpReward, $"stage:{stageId}");
        }

        _logger.LogInformation(
            "스테이지 클리어 완료 — PlayerId: {PlayerId}, StageId: {StageId}, IsFirst: {IsFirst}, ClearCount: {Count}",
            playerId, stageId, isFirstClear, clearCount);

        return new StageClearResponseDto(
            IsFirstClear: isFirstClear,
            ClearCount: clearCount,
            ExpGranted: stage.ExpReward,
            FirstRewardMessage: firstRewardMessage,
            ReplayRewardMessage: replayRewardMessage
        );
    }

    // 최초 클리어 보상 지급 헬퍼
    private async Task<string?> GrantStageRewardAsync(
        int playerId, string tableCode, string sourceKey, string mailTitle, string mailBody)
    {
        var table = await _rewardTableRepo.FindAsync(RewardSourceType.StageComplete, tableCode);
        if (table is null)
        {
            _logger.LogDebug("스테이지 보상 테이블 없음 — Code: {Code}", tableCode);
            return null;
        }

        var bundle = BuildBundle(table);
        if (bundle.IsEmpty) return null;

        var result = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.StageComplete,
            SourceKey: sourceKey,
            Bundle: bundle,
            MailTitle: mailTitle,
            MailBody: mailBody
        ));

        return result.Success ? "보상이 지급되었습니다." : null;
    }

    // 재클리어 보상 지급 — 감소율 계산 후 아이템 수량에 적용
    // 실제 지급량 = floor(기본 수량 * max(0.5, 1.0 - (clearCount-1) * decayPercent/100))
    private async Task<string?> GrantReplayRewardAsync(
        int playerId, Domain.Content.Entities.Stage stage, int clearCount, string sourceKey)
    {
        var table = await _rewardTableRepo.FindAsync(RewardSourceType.StageComplete, stage.RePlayRewardTableCode!);
        if (table is null)
        {
            _logger.LogDebug("재클리어 보상 테이블 없음 — Code: {Code}", stage.RePlayRewardTableCode);
            return null;
        }

        // 감소율 계산 — clearCount=2부터 감소 시작 (첫 재클리어)
        var decayFactor = stage.RePlayRewardDecayPercent > 0
            ? Math.Max(0.5, 1.0 - (clearCount - 1) * stage.RePlayRewardDecayPercent / 100.0)
            : 1.0;

        // 아이템 수량에 감소율 적용
        var items = table.Entries
            .Select(e => new RewardItem(e.ItemId, Math.Max(1, (int)(e.Count * decayFactor))))
            .ToList();

        var bundle = new RewardBundle(Items: items);
        if (bundle.IsEmpty) return null;

        var result = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.StageComplete,
            SourceKey: sourceKey,
            Bundle: bundle,
            MailTitle: $"스테이지 {stage.Name} 재클리어 보상",
            MailBody: $"재클리어 {clearCount}회 보상을 수령하세요."
        ));

        return result.Success ? $"재클리어 보상이 지급되었습니다. (감소율 {100 - (int)(decayFactor * 100)}%)" : null;
    }

    // RewardTable → RewardBundle 변환 헬퍼
    private static RewardBundle BuildBundle(Framework.Domain.Entities.RewardTable table)
    {
        var items = table.Entries
            .Select(e => new RewardItem(e.ItemId, e.Count))
            .ToList();
        return new RewardBundle(Items: items);
    }

    // 플레이어 스테이지 진행 현황 조회
    public async Task<List<StageProgressDto>> GetProgressAsync(int playerId)
    {
        // 활성 스테이지 전체 조회
        var stages = await _stageRepo.GetAllActiveAsync();
        // 플레이어 클리어 기록 조회
        var clears = await _clearRepo.GetByPlayerIdAsync(playerId);
        var clearMap = clears.ToDictionary(c => c.StageId);

        // 각 스테이지별 잠금 여부 계산
        return stages.Select(s =>
        {
            var isLocked = s.RequiredPrevStageId.HasValue &&
                           !clearMap.ContainsKey(s.RequiredPrevStageId.Value);
            clearMap.TryGetValue(s.Id, out var clear);

            return new StageProgressDto(
                StageId: s.Id,
                Code: s.Code,
                Name: s.Name,
                IsCleared: clear is not null,
                ClearCount: clear?.ClearCount ?? 0,
                BestScore: clear?.BestScore ?? 0,
                BestStars: clear?.BestStars ?? 0,
                BestClearTimeMs: clear?.BestClearTimeMs ?? 0,
                IsLocked: isLocked,
                SortOrder: s.SortOrder
            );
        }).OrderBy(s => s.SortOrder).ToList();
    }

    // 활성 스테이지 목록 조회
    public async Task<List<StageDto>> GetActiveStagesAsync()
    {
        var stages = await _stageRepo.GetAllActiveAsync();
        return stages.Select(ToDto).ToList();
    }

    // Admin — 스테이지 생성
    public async Task<StageDto> CreateStageAsync(CreateStageDto dto)
    {
        var stage = new Domain.Content.Entities.Stage
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            RewardTableCode = dto.RewardTableCode?.Trim(),
            RePlayRewardTableCode = dto.RePlayRewardTableCode?.Trim(),
            RePlayRewardDecayPercent = dto.RePlayRewardDecayPercent,
            ExpReward = dto.ExpReward,
            RequiredPrevStageId = dto.RequiredPrevStageId,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder
        };

        await _stageRepo.AddAsync(stage);
        await _stageRepo.SaveChangesAsync();

        _logger.LogInformation("스테이지 생성 — Id: {Id}, Code: {Code}", stage.Id, stage.Code);
        return ToDto(stage);
    }

    // Admin — 스테이지 수정
    public async Task<bool> UpdateStageAsync(int id, UpdateStageDto dto)
    {
        var stage = await _stageRepo.GetByIdAsync(id);
        if (stage is null) return false;

        stage.Name = dto.Name.Trim();
        stage.RewardTableCode = dto.RewardTableCode?.Trim();
        stage.RePlayRewardTableCode = dto.RePlayRewardTableCode?.Trim();
        stage.RePlayRewardDecayPercent = dto.RePlayRewardDecayPercent;
        stage.ExpReward = dto.ExpReward;
        stage.RequiredPrevStageId = dto.RequiredPrevStageId;
        stage.IsActive = dto.IsActive;
        stage.SortOrder = dto.SortOrder;

        await _stageRepo.SaveChangesAsync();
        return true;
    }

    // Admin — 전체 스테이지 검색 (페이지네이션)
    public async Task<(List<StageDto> Items, int TotalCount)> SearchAsync(
        string? keyword, int page, int pageSize)
    {
        var (items, total) = await _stageRepo.SearchAsync(keyword, page, pageSize);
        return (items.Select(ToDto).ToList(), total);
    }

    // Admin — 단건 조회
    public async Task<StageDto?> GetByIdAsync(int id)
    {
        var stage = await _stageRepo.GetByIdAsync(id);
        return stage is null ? null : ToDto(stage);
    }

    // 엔티티 → DTO 변환 헬퍼
    private static StageDto ToDto(Domain.Content.Entities.Stage s) => new(
        s.Id, s.Code, s.Name,
        s.RewardTableCode, s.RePlayRewardTableCode, s.RePlayRewardDecayPercent,
        s.ExpReward, s.RequiredPrevStageId, s.IsActive, s.SortOrder
    );
}
