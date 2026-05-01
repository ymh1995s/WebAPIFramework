using Framework.Application.Features.Reward;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.AdReward;

// 광고 SSV 보상 처리 서비스 구현체
// [파이프라인] 검증기 선택 → 서명 검증 → 정책 조회 → 일일 한도 체크 → RewardDispatcher 지급
public class AdRewardService : IAdRewardService
{
    private readonly IAdNetworkVerifierResolver _resolver;
    private readonly IAdPolicyRepository _policyRepo;
    private readonly IRewardGrantRepository _grantRepo;
    private readonly IRewardTableRepository _tableRepo;
    private readonly IRewardDispatcher _dispatcher;
    private readonly ILogger<AdRewardService> _logger;

    public AdRewardService(
        IAdNetworkVerifierResolver resolver,
        IAdPolicyRepository policyRepo,
        IRewardGrantRepository grantRepo,
        IRewardTableRepository tableRepo,
        IRewardDispatcher dispatcher,
        ILogger<AdRewardService> logger)
    {
        _resolver = resolver;
        _policyRepo = policyRepo;
        _grantRepo = grantRepo;
        _tableRepo = tableRepo;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    // 광고 SSV 콜백 전체 처리 — 검증 → 정책 조회 → 한도 체크 → 보상 지급
    public async Task<AdRewardResult> ProcessCallbackAsync(AdNetworkType network, AdCallbackContext ctx)
    {
        // 1단계: 해당 네트워크 검증기 선택
        IAdNetworkVerifier verifier;
        try
        {
            verifier = _resolver.Resolve(network);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "지원하지 않는 광고 네트워크: {Network}", network);
            return AdRewardResult.Fail($"지원하지 않는 광고 네트워크: {network}");
        }

        // 2단계: 서명 검증 및 파라미터 파싱 — 검증 실패 시 예외가 Controller로 전파됨
        var verified = await verifier.VerifyAsync(ctx);

        _logger.LogInformation(
            "광고 콜백 검증 성공 — Network: {Network}, PlayerId: {PlayerId}, PlacementId: {PlacementId}, TxId: {TxId}",
            network, verified.PlayerId, verified.PlacementId, verified.TransactionId);

        // 3단계: 광고 정책 조회
        var policy = await _policyRepo.FindAsync(network, verified.PlacementId);
        if (policy is null)
        {
            _logger.LogWarning(
                "광고 정책 없음 — Network: {Network}, PlacementId: {PlacementId}",
                network, verified.PlacementId);
            throw new AdPolicyNotFoundException(network.ToString(), verified.PlacementId);
        }

        // 정책 비활성화 상태이면 보상 미지급
        if (!policy.IsEnabled)
        {
            _logger.LogInformation(
                "광고 정책 비활성화 — PolicyId: {PolicyId}, PlacementId: {PlacementId}",
                policy.Id, verified.PlacementId);
            return AdRewardResult.Fail("비활성화된 광고 정책입니다.");
        }

        // 4단계: 일일 한도 체크 (DailyLimit > 0인 경우만)
        if (policy.DailyLimit > 0)
        {
            // 오늘 UTC 00:00 기준 지급 횟수 조회
            var utcToday = DateTime.UtcNow.Date;
            // SourceKey 패턴: "{network}:{placementId}:{transactionId}" — PlacementId별 카운트
            var sourceKeyPrefix = $"{network}:{verified.PlacementId}:";
            var todayCount = await _grantRepo.CountTodayAsync(
                verified.PlayerId, RewardSourceType.AdReward, sourceKeyPrefix, utcToday);

            if (todayCount >= policy.DailyLimit)
            {
                _logger.LogInformation(
                    "광고 일일 한도 초과 — PlayerId: {PlayerId}, PlacementId: {PlacementId}, 오늘 지급: {Count}/{Limit}",
                    verified.PlayerId, verified.PlacementId, todayCount, policy.DailyLimit);
                throw new AdDailyLimitExceededException(verified.PlayerId, verified.PlacementId, policy.DailyLimit);
            }
        }

        // 5단계: 보상 번들 구성 (RewardTable이 연결된 경우)
        var bundle = await BuildBundleAsync(policy.RewardTableId);

        // 보상 테이블이 없으면 지급 없이 성공 처리 (광고 시청 추적만)
        if (bundle.IsEmpty)
        {
            _logger.LogInformation(
                "광고 보상 없음 (RewardTable 미연결) — PolicyId: {PolicyId}", policy.Id);
            return AdRewardResult.Ok(verified.PlayerId);
        }

        // 6단계: RewardDispatcher를 통해 보상 지급
        // SourceKey = "{network}:{placementId}:{transactionId}" — PlacementId별 멱등성 + 한도 카운트
        var sourceKey = $"{network}:{verified.PlacementId}:{verified.TransactionId}";
        var grantRequest = new GrantRewardRequest(
            PlayerId: verified.PlayerId,
            SourceType: RewardSourceType.AdReward,
            SourceKey: sourceKey,
            Bundle: bundle,
            MailTitle: "광고 시청 보상",
            MailBody: "광고 시청에 감사드립니다. 보상을 수령해 주세요.",
            Mode: DispatchMode.Direct,
            // 플레이어가 광고를 시청하여 획득한 보상 — 행위자는 Player
            ActorType: AuditActorType.Player,
            ActorId: verified.PlayerId
        );

        var grantResult = await _dispatcher.GrantAsync(grantRequest);

        if (grantResult.AlreadyGranted)
        {
            _logger.LogInformation(
                "광고 보상 중복 콜백 — PlayerId: {PlayerId}, SourceKey: {SourceKey}",
                verified.PlayerId, sourceKey);
            return AdRewardResult.Duplicate(verified.PlayerId);
        }

        if (!grantResult.Success)
        {
            _logger.LogError(
                "광고 보상 지급 실패 — PlayerId: {PlayerId}, 이유: {Message}",
                verified.PlayerId, grantResult.Message);
            return AdRewardResult.Fail(grantResult.Message);
        }

        return AdRewardResult.Ok(verified.PlayerId);
    }

    // RewardTable의 항목으로 RewardBundle 구성
    // Weight가 있는 항목은 가중치 확률 추첨, 없으면 전체 고정 지급
    private async Task<RewardBundle> BuildBundleAsync(int? rewardTableId)
    {
        if (rewardTableId is null)
            return new RewardBundle();

        // ID로 RewardTable + Entries 조회
        var table = await _tableRepo.GetByIdWithEntriesAsync(rewardTableId.Value);
        if (table is null || table.IsDeleted)
            return new RewardBundle();

        var entries = table.Entries.ToList();
        if (entries.Count == 0)
            return new RewardBundle();

        // Weight가 있는 항목이 있으면 확률 추첨, 없으면 전체 고정 지급
        bool hasWeight = entries.Any(e => e.Weight.HasValue);

        if (hasWeight)
        {
            // 가중치 기반 확률 추첨 — 하나의 항목만 선택
            var totalWeight = entries.Sum(e => e.Weight ?? 0);
            if (totalWeight <= 0)
                return new RewardBundle();

            var roll = Random.Shared.Next(totalWeight);
            var cumulative = 0;
            foreach (var entry in entries)
            {
                cumulative += entry.Weight ?? 0;
                if (roll < cumulative)
                {
                    return new RewardBundle(Items: new[]
                    {
                        new RewardItem(entry.ItemId, entry.Count)
                    });
                }
            }
        }
        else
        {
            // 전체 고정 지급 — Weight 없는 모든 항목 지급
            var items = entries
                .Select(e => new RewardItem(e.ItemId, e.Count))
                .ToArray();
            return new RewardBundle(Items: items);
        }

        return new RewardBundle();
    }
}
