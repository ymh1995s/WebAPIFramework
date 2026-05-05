using Framework.Application.Common;
using Framework.Application.Features.Reward;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Exp;

// 경험치 처리 서비스 — 경험치 추가 및 레벨업 보상 지급 담당
// 레벨업 시 IRewardDispatcher를 통해 레벨업 보상 지급
// 레벨 계산은 ILevelTableProvider를 통해 DB 기반 동적 테이블 사용
public class ExpService : IExpService
{
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IRewardTableRepository _rewardTableRepo;
    private readonly ILevelTableProvider _levelTable;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpService> _logger;

    public ExpService(
        IPlayerProfileRepository profileRepo,
        IRewardDispatcher rewardDispatcher,
        IRewardTableRepository rewardTableRepo,
        ILevelTableProvider levelTable,
        IUnitOfWork unitOfWork,
        ILogger<ExpService> logger)
    {
        _profileRepo = profileRepo;
        _rewardDispatcher = rewardDispatcher;
        _rewardTableRepo = rewardTableRepo;
        _levelTable = levelTable;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 경험치 추가 — 레벨업 발생 시 레벨업 보상도 연속 처리
    // [흐름] 프로필 조회 → Exp 추가 → 레벨업 while 루프 → SaveChanges → 레벨업 보상 지급
    // 프로필 변경과 레벨업 보상 지급까지 하나의 트랜잭션으로 묶어 원자성 보장
    public async Task AddExpAsync(int playerId, int expAmount, string sourceKey)
    {
        // 경험치가 0 이하면 처리 불필요
        if (expAmount <= 0) return;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 플레이어 프로필 조회
            var profile = await _profileRepo.GetByPlayerIdAsync(playerId);
            if (profile is null)
            {
                _logger.LogWarning("ExpService: PlayerProfile 없음 — PlayerId: {PlayerId}", playerId);
                return;
            }

            // 최대 레벨이면 경험치 추가 생략
            if (profile.Level >= _levelTable.MaxLevel)
            {
                _logger.LogDebug("ExpService: 최대 레벨 도달, Exp 추가 생략 — PlayerId: {PlayerId}", playerId);
                return;
            }

            // 경험치 누적
            profile.Exp += expAmount;
            profile.UpdatedAt = DateTime.UtcNow;

            // 레벨업 대상 레벨 수집 — 다중 레벨업 지원 (while 루프)
            var leveledUp = new List<int>();
            while (profile.Level < _levelTable.MaxLevel &&
                   profile.Exp >= _levelTable.GetThreshold(profile.Level + 1))
            {
                profile.Level++;
                leveledUp.Add(profile.Level);
                _logger.LogInformation(
                    "레벨업 — PlayerId: {PlayerId}, Level: {Level}", playerId, profile.Level);
            }

            // 프로필 저장 (Exp + Level 반영)
            await _profileRepo.UpdateAsync(profile);

            // 레벨업 보상 지급 — 레벨별로 RewardTable 조회 후 지급
            foreach (var level in leveledUp)
            {
                await GrantLevelUpRewardAsync(playerId, level);
            }
        });
    }

    // 레벨업 보상 지급 — 레벨업 보상 테이블에서 번들을 조회하여 지급
    // SourceType=LevelUp, SourceKey="levelup:{level}"
    private async Task GrantLevelUpRewardAsync(int playerId, int level)
    {
        // 레벨업 보상 테이블 조회 (Code = SourceKeys.LevelUp)
        var tableCode = SourceKeys.LevelUp(level);
        var table = await _rewardTableRepo.FindAsync(RewardSourceType.LevelUp, tableCode);

        if (table is null)
        {
            // 보상 테이블 없으면 지급 없이 스킵 (설정 안 된 레벨은 무보상)
            _logger.LogDebug(
                "레벨업 보상 테이블 없음 — PlayerId: {PlayerId}, Level: {Level}, Code: {Code}",
                playerId, level, tableCode);
            return;
        }

        // 보상 번들 구성 — 테이블 항목 전체를 아이템으로 지급
        // 경험치는 레벨업 보상으로 지급하지 않음 (순환 방지)
        var items = table.Entries
            .Select(e => new RewardItem(e.ItemId, e.Count))
            .ToList();

        var bundle = new RewardBundle(Items: items);
        if (bundle.IsEmpty) return;

        // 보상 지급 (RewardDispatcher를 통한 멱등 지급)
        var request = new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.LevelUp,
            SourceKey: SourceKeys.LevelUp(level),
            Bundle: bundle,
            MailTitle: $"레벨 {level} 달성 보상",
            MailBody: $"레벨 {level}에 도달하셨습니다. 보상을 수령하세요."
        );

        var result = await _rewardDispatcher.GrantAsync(request);
        if (!result.Success && !result.AlreadyGranted)
        {
            _logger.LogWarning(
                "레벨업 보상 지급 실패 — PlayerId: {PlayerId}, Level: {Level}, Message: {Message}",
                playerId, level, result.Message);
        }
    }
}
