using Framework.Application.Common;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Quest;
using Framework.Application.Features.Reward;
using Framework.Domain.Constants;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.ItemUse;

// 아이템 사용(소모) 서비스 구현체
// [처리 흐름]
// 1. 플레이어 아이템 보유 여부 조회
// 2. 수량 검증 (0 이하면 NotEnoughQuantity 반환)
// 3. 수량 차감
// 4. UseRewardTableId != null이면 보상 지급 (중복 요청은 Duplicate 반환, 빈 테이블은 RewardTableEmpty 반환)
//    — 실패 분기에서는 수량 복원 후 조기 반환
// 5. 감사 로그 기록 (모든 성공 경로의 합류 지점에서 기록, 차감이므로 음수)
// 6. 등록된 IItemUseEffectExtension 순차 실행
// 7. SaveChanges 커밋
// [동시성] xmin 낙관적 동시성 충돌 시 최대 3회 재시도 (ClearChangeTracker 후 재조회)
public class ItemUseService : IItemUseService
{
    private readonly IPlayerItemRepository _playerItemRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IRewardTableRepository _rewardTableRepository;
    private readonly IRewardDispatcher _rewardDispatcher;
    private readonly IAuditLogService _auditLogService;
    private readonly IQuestProgressService _questProgressService;
    private readonly IUnitOfWork _unitOfWork;
    // IEnumerable 주입 — 등록된 구현체가 없어도 빈 컬렉션으로 안전하게 처리됨
    private readonly IEnumerable<IItemUseEffectExtension> _effects;
    private readonly ILogger<ItemUseService> _logger;

    public ItemUseService(
        IPlayerItemRepository playerItemRepository,
        IItemRepository itemRepository,
        IRewardTableRepository rewardTableRepository,
        IRewardDispatcher rewardDispatcher,
        IAuditLogService auditLogService,
        IQuestProgressService questProgressService,
        IUnitOfWork unitOfWork,
        IEnumerable<IItemUseEffectExtension> effects,
        ILogger<ItemUseService> logger)
    {
        _playerItemRepository = playerItemRepository;
        _itemRepository = itemRepository;
        _rewardTableRepository = rewardTableRepository;
        _rewardDispatcher = rewardDispatcher;
        _auditLogService = auditLogService;
        _questProgressService = questProgressService;
        _unitOfWork = unitOfWork;
        _effects = effects;
        _logger = logger;
    }

    // 아이템 사용 처리 — 수량 차감(quantity개) → 감사 로그 → 보상 지급(옵션) → 확장 효과(옵션) → 커밋
    // [동시성] DbUpdateConcurrencyException 발생 시 ChangeTracker 초기화 후 최대 3회 재시도
    // [N개 사용] quantity > 1 시 보유량 검증, 차감, 감사 로그, 보상 번들 모두 quantity 배율 적용
    public async Task<ItemUseResult> UseItemAsync(int playerId, int itemId, string clientRequestId, int quantity = 1, CancellationToken ct = default)
    {
        // xmin 낙관적 동시성 충돌 시 재시도 루프 — 최대 3회
        ItemUseResult? finalResult = null;
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                finalResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // 1단계: 플레이어 아이템 보유 여부 조회
                    var playerItem = await _playerItemRepository.GetByPlayerAndItemAsync(playerId, itemId);
                    if (playerItem is null)
                    {
                        _logger.LogWarning("아이템 사용 실패 — 미보유: PlayerId={PlayerId}, ItemId={ItemId}", playerId, itemId);
                        return new ItemUseResult(ItemUseResultStatus.ItemNotFound);
                    }

                    // 2단계: 수량 검증 — 요청 수량(quantity) 이상이어야 사용 가능
                    if (playerItem.Quantity < quantity)
                    {
                        _logger.LogWarning("아이템 사용 실패 — 수량 부족: PlayerId={PlayerId}, ItemId={ItemId}, 보유={Qty}, 요청={ReqQty}", playerId, itemId, playerItem.Quantity, quantity);
                        return new ItemUseResult(ItemUseResultStatus.NotEnoughQuantity);
                    }

                    // 3단계: 수량 차감 — 실패 시 각 분기에서 복원하고 조기 반환
                    var quantityBefore = playerItem.Quantity;
                    playerItem.Quantity -= quantity;

                    // 4단계: 아이템 마스터 조회 — UseRewardTableId 확인용
                    var item = await _itemRepository.GetByIdAsync(itemId);

                    // [주의] UseRewardTableId가 null이면 RewardDispatcher를 호출하지 않으므로
                    // clientRequestId 기반 멱등성이 보장되지 않음.
                    // 이는 보상 없는 소모품에 해당하며, 네트워크 재시도 시 중복 차감이 발생할 수 있음.
                    // 실제 사용 사례가 발생하면 별도 중복 검출 메커니즘 추가 필요.
                    if (item?.UseRewardTableId is not null)
                    {
                        // 보상 테이블 항목을 번들로 변환하여 직접 지급 — quantity 배율 적용
                        var bundle = await BuildBundleFromTableAsync(item.UseRewardTableId.Value, quantity);

                        // 보상 테이블이 비어 있으면 수량 차감 취소 후 RewardTableEmpty 반환
                        if (bundle.IsEmpty)
                        {
                            // 수량 복원 — 빈 보상 테이블로 인해 사용 처리 불가
                            playerItem.Quantity += quantity;
                            _logger.LogWarning("아이템 사용 실패 — 보상 테이블 비어있음: PlayerId={PlayerId}, ItemId={ItemId}, RewardTableId={TableId}",
                                playerId, itemId, item.UseRewardTableId.Value);
                            return new ItemUseResult(ItemUseResultStatus.RewardTableEmpty);
                        }

                        var grantResult = await _rewardDispatcher.GrantAsync(new GrantRewardRequest(
                            PlayerId: playerId,
                            SourceType: RewardSourceType.ItemUse,
                            SourceKey: SourceKeys.ItemUse(playerId, clientRequestId),
                            Bundle: bundle,
                            Mode: DispatchMode.Direct
                        ));

                        if (grantResult.AlreadyGranted)
                        {
                            // 동일 clientRequestId로 이미 처리된 요청 — 수량 복원 후 Duplicate 반환
                            playerItem.Quantity += quantity;  // 수량 복원 — 보상 미지급 시 차감 취소
                            _logger.LogWarning("아이템 사용 중복 요청 — PlayerId={PlayerId}, ItemId={ItemId}, ClientRequestId={Req}", playerId, itemId, clientRequestId);
                            return new ItemUseResult(ItemUseResultStatus.Duplicate);
                        }

                        if (!grantResult.Success)
                        {
                            // 보상 지급 실패 — 수량 복원 후 RewardGrantFailed 반환
                            playerItem.Quantity += quantity;
                            _logger.LogError("아이템 사용 보상 지급 실패 — PlayerId={PlayerId}, ItemId={ItemId}, 사유={Msg}", playerId, itemId, grantResult.Message);
                            return new ItemUseResult(ItemUseResultStatus.RewardGrantFailed);
                        }
                    }

                    // 5단계: 감사 로그 기록 — 모든 실패 분기(Duplicate/RewardTableEmpty/!Success) 처리 이후
                    // UseRewardTableId 유무에 관계없이 이 합류 지점에서 기록 (차감이므로 변동량은 음수)
                    await _auditLogService.RecordAsync(
                        playerId,
                        itemId,
                        reason: AuditLogReasons.ItemUse,
                        changeAmount: -quantity,
                        balanceBefore: quantityBefore,
                        balanceAfter: playerItem.Quantity);

                    // 6단계: 게임 특화 확장 효과 순차 실행 (등록된 구현체가 없으면 건너뜀)
                    // ItemUseContext에 Quantity 전달 — 확장 효과가 사용 수량 기반 분기 가능
                    var effectsList = _effects.ToList();
                    if (effectsList.Count > 0)
                    {
                        var context = new ItemUseContext(
                            PlayerId: playerId,
                            ItemId: itemId,
                            ItemType: (int)(item?.ItemType ?? 0),
                            Quantity: quantity
                        );
                        foreach (var effect in effectsList)
                        {
                            await effect.ApplyAsync(context, ct);
                        }
                    }

                    // 7단계: 변경사항 저장 — 수량 차감 커밋
                    await _playerItemRepository.SaveChangesAsync();

                    _logger.LogInformation("아이템 사용 완료 — PlayerId={PlayerId}, ItemId={ItemId}, 수량={Qty}", playerId, itemId, quantity);
                    return new ItemUseResult(ItemUseResultStatus.Success);
                });

                // 성공 시 루프 탈출
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                // 마지막 시도에서도 충돌 발생 시 예외를 rethrow하여 호출자에게 전파
                if (attempt >= maxAttempts)
                {
                    _logger.LogWarning(
                        "아이템 사용 xmin 동시성 충돌 한도 초과 — 재시도 {Max}회 모두 실패 (PlayerId={PlayerId}, ItemId={ItemId})",
                        maxAttempts, playerId, itemId);
                    throw;
                }

                // xmin 충돌 — stale 엔티티 제거 후 재시도
                _logger.LogWarning(
                    "아이템 사용 xmin 동시성 충돌 — 재시도 {Attempt}/{Max} (PlayerId={PlayerId}, ItemId={ItemId})",
                    attempt, maxAttempts, playerId, itemId);
                _unitOfWork.ClearChangeTracker();
                finalResult = null;
            }
        }

        // 이 지점은 도달 불가 (위 루프에서 반드시 break 또는 throw) — 컴파일러 만족용
        if (finalResult is null)
            throw new InvalidOperationException("아이템 사용 재시도 루프가 비정상 종료되었습니다.");

        // 트랜잭션 커밋 후 퀘스트 카운터 증가 — 성공한 경우에만 호출
        if (finalResult.Status == ItemUseResultStatus.Success)
            await _questProgressService.IncrementAsync(playerId, QuestConditionType.ItemUsed, quantity, itemId);

        return finalResult;
    }

    // 보상 테이블 ID로 RewardBundle 구성 — quantity 배율 적용
    // RewardTable의 Entries(ItemId + Quantity 쌍)를 RewardItem 리스트로 변환
    // quantity > 1이면 각 항목 수량에 배율을 곱하여 N개 사용에 대응
    private async Task<RewardBundle> BuildBundleFromTableAsync(int rewardTableId, int quantity = 1)
    {
        var table = await _rewardTableRepository.GetByIdWithEntriesAsync(rewardTableId);
        if (table is null || table.Entries.Count == 0)
            return new RewardBundle();

        // 각 항목을 RewardItem으로 변환 — quantity 배율 적용 (checked로 오버플로우 감지)
        var rewardItems = table.Entries
            .Select(e => new RewardItem(e.ItemId, checked(e.Count * quantity)))
            .ToList();

        return new RewardBundle(Items: rewardItems);
    }
}
