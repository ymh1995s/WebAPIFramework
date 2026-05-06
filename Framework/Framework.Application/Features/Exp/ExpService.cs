using Framework.Application.Common;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Exp;

// 경험치 처리 서비스 — 경험치 추가 및 레벨업 감지 담당
// 레벨업 보상 지급은 호출자(RewardDispatcher)가 반환값으로 처리 — 순환 의존 없음
// 레벨 계산은 ILevelTableProvider를 통해 DB 기반 동적 테이블 사용
public class ExpService : IExpService
{
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly ILevelTableProvider _levelTable;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpService> _logger;

    public ExpService(
        IPlayerProfileRepository profileRepo,
        ILevelTableProvider levelTable,
        IUnitOfWork unitOfWork,
        ILogger<ExpService> logger)
    {
        _profileRepo = profileRepo;
        _levelTable = levelTable;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 경험치 추가 — 레벨업 발생 시 도달한 레벨 번호 목록을 오름차순으로 반환
    // [흐름] 프로필 조회 → Exp 추가 → 레벨업 while 루프 → SaveChanges → 오른 레벨 목록 반환
    // [분리] 레벨업 보상 지급은 호출자(RewardDispatcher)가 GrantLevelUpRewardsAsync로 처리
    public async Task<IReadOnlyList<int>> AddExpAsync(int playerId, int expAmount, string sourceKey)
    {
        // 경험치가 0 이하면 처리 불필요 — 빈 리스트 반환
        if (expAmount <= 0) return Array.Empty<int>();

        // 트랜잭션 결과를 클로저 변수로 외부에 노출 (제네릭 오버로드 사용)
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // 플레이어 프로필 조회
            var profile = await _profileRepo.GetByPlayerIdAsync(playerId);
            if (profile is null)
            {
                _logger.LogWarning("ExpService: PlayerProfile 없음 — PlayerId: {PlayerId}", playerId);
                return Array.Empty<int>();
            }

            // 최대 레벨이면 경험치 추가 생략
            if (profile.Level >= _levelTable.MaxLevel)
            {
                _logger.LogDebug("ExpService: 최대 레벨 도달, Exp 추가 생략 — PlayerId: {PlayerId}", playerId);
                return Array.Empty<int>();
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

            // 오른 레벨 번호 목록 반환 — 호출자가 레벨업 보상 지급 처리
            return (IReadOnlyList<int>)leveledUp;
        });
    }
}
