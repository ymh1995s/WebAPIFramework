using System.Text.Json;
using Framework.Application.Common;
using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Exp;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Reward;

// 보상 지급 단일 진입점 구현체
// [파이프라인] 트랜잭션 시작 → RewardGrant 선기록(원자적 중복 차단) → Direct/Mail 분기 → MailId 업데이트 → 커밋
// [원자성] IUnitOfWork를 통해 전체 흐름을 단일 DB 트랜잭션으로 묶어 부분 성공 상태를 방지
// [멱등성] UNIQUE(PlayerId, SourceType, SourceKey) 제약 위반 catch로 레이스 컨디션 완전 차단
// [순환 의존] RewardDispatcher → ExpService → RewardDispatcher 구조이나 레벨업 보상은 Exp=0 이므로 무한 루프 불발
public class RewardDispatcher : IRewardDispatcher
{
    private readonly IRewardGrantRepository _grantRepo;
    private readonly IPlayerRepository _playerRepository;
    private readonly IExpService _expService;
    private readonly IPlayerItemRepository _itemRepo;
    private readonly IMailRepository _mailRepo;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RewardDispatcher> _logger;

    public RewardDispatcher(
        IRewardGrantRepository grantRepo,
        IPlayerRepository playerRepository,
        IExpService expService,
        IPlayerItemRepository itemRepo,
        IMailRepository mailRepo,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<RewardDispatcher> logger)
    {
        _grantRepo = grantRepo;
        _playerRepository = playerRepository;
        _expService = expService;
        _itemRepo = itemRepo;
        _mailRepo = mailRepo;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 보상 지급 — 공통 파이프라인 진입점
    // [순서] 번들 검증 → PlayerId 확인 → 재시도 루프 → 트랜잭션 스코프 → 선기록 → 지급 → MailId 업데이트
    // [원자성] ExecuteInTransactionAsync로 전체 흐름을 단일 트랜잭션 스코프로 묶음
    // [중첩 트랜잭션 지원] 외부 트랜잭션 활성 시 자동으로 참여자가 되어 커밋/롤백을 호출자에게 위임
    // [동시성 보호] PlayerItem.xmin 낙관적 동시성 토큰 — 충돌 시 최대 3회 재시도
    public async Task<GrantRewardResult> GrantAsync(GrantRewardRequest request)
    {
        // 빈 번들이면 지급할 내용 없음 (재시도 루프 진입 전 사전 검증)
        if (request.Bundle.IsEmpty)
            return GrantRewardResult.Fail("지급할 보상이 없습니다.");

        // PlayerId 존재 여부 확인 — FK 위반 방지 (트랜잭션 시작 전 검증, 재시도 루프 밖)
        var player = await _playerRepository.GetByIdAsync(request.PlayerId);
        if (player is null)
            return GrantRewardResult.NotFound();

        // PlayerItem.xmin 동시성 충돌 시 재시도 루프 — 최대 3회
        // DbUpdateConcurrencyException은 ExecuteInTransactionAsync 내부에서 롤백 후 여기로 전파됨
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // 전체 보상 지급 흐름을 단일 트랜잭션 스코프로 묶음
                // 외부 트랜잭션이 활성 상태이면 자동으로 참여자가 되어 커밋/롤백을 호출자에게 위임
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // 1단계: RewardGrant 선기록 — UNIQUE 제약으로 동시 요청 중복 지급 원자적 차단
                    var grant = new Domain.Entities.RewardGrant
                    {
                        PlayerId = request.PlayerId,
                        SourceType = request.SourceType,
                        SourceKey = request.SourceKey,
                        GrantedAt = DateTime.UtcNow,
                        BundleSnapshot = JsonSerializer.Serialize(request.Bundle)
                    };
                    await _grantRepo.AddAsync(grant);

                    try
                    {
                        await _grantRepo.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                    {
                        // UNIQUE 위반 — 이미 지급된 보상
                        // DetachEntry로 실패 엔티티를 ChangeTracker에서 제거 — 미제거 시 람다 종료 후 SaveChangesAsync에서 재시도됨
                        _unitOfWork.DetachEntry(grant);
                        _logger.LogInformation(
                            "이미 지급된 보상 (선기록 중복) — PlayerId: {PlayerId}, SourceType: {SourceType}, SourceKey: {SourceKey}",
                            request.PlayerId, request.SourceType, request.SourceKey);
                        return GrantRewardResult.Duplicate();
                    }

                    // 2단계: 지급 방식 결정
                    var mode = DetermineMode(request.Mode, request.Bundle);

                    // 3단계: 실제 보상 지급 — 예외 발생 시 람다 밖으로 전파, 소유자가 롤백
                    GrantRewardResult result;
                    if (mode == DispatchMode.Direct)
                        result = await DispatchDirectAsync(request);
                    else
                        result = await DispatchMailAsync(request);

                    // 4단계: MailId 업데이트
                    if (result.MailId.HasValue)
                    {
                        grant.MailId = result.MailId;
                        await _grantRepo.SaveChangesAsync();
                    }

                    _logger.LogInformation(
                        "보상 지급 완료 — PlayerId: {PlayerId}, SourceType: {SourceType}, Mode: {Mode}",
                        request.PlayerId, request.SourceType, mode);

                    return result;
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                // when 필터 금지 — 마지막 시도(attempt=maxAttempts)도 반드시 catch에 진입해야 Fail 반환 가능
                // when (attempt < maxAttempts) 필터를 사용하면 마지막 시도 예외가 호출자로 전파되어 500 에러 발생
                if (attempt >= maxAttempts)
                {
                    _logger.LogWarning(
                        "PlayerItem 동시성 충돌 한도 초과 — 재시도 {Max}회 모두 실패 (PlayerId={PlayerId}, SourceKey={SourceKey})",
                        maxAttempts, request.PlayerId, request.SourceKey);
                    // retry loop를 빠져나가 아래 Fail 반환부로 이동
                    break;
                }

                // PlayerItem.xmin 충돌 — 다른 트랜잭션이 동일 PlayerItem 행을 먼저 갱신
                // ClearChangeTracker로 stale 엔티티 제거 후 다음 시도에서 DB 최신값으로 재조회
                _logger.LogWarning(
                    "PlayerItem 동시성 충돌 — 재시도 {Attempt}/{Max} (PlayerId={PlayerId}, SourceKey={SourceKey})",
                    attempt, maxAttempts, request.PlayerId, request.SourceKey);
                _unitOfWork.ClearChangeTracker();
            }
        }

        // 최대 재시도 횟수 초과 — 지속적인 동시성 충돌로 지급 실패
        return GrantRewardResult.Fail("동시성 충돌이 지속되어 보상 지급에 실패했습니다.");
    }

    // 지급 방식 결정 — Auto이면 번들 구성에 따라 자동 판단
    // Items가 있으면 Mail, Items가 없고 Exp만 있으면 Direct
    private static DispatchMode DetermineMode(DispatchMode requested, RewardBundle bundle)
    {
        if (requested != DispatchMode.Auto) return requested;

        // 아이템이 있으면 Mail, Exp만 있으면 Direct
        return bundle.IsCurrencyOnly ? DispatchMode.Direct : DispatchMode.Mail;
    }

    // Direct 지급 — Currency-as-Item 방식
    // Gold(ItemId=1) / Gems(ItemId=2): 호출자가 Items 목록에 포함하여 전달 — 별도 변환 없음
    // Exp: PlayerProfile.Exp에 직접 증가 (레벨업 로직 포함)
    // 트랜잭션 내부에서 호출되므로 SaveChangesAsync()는 중간 flush 역할 (커밋 아님)
    private async Task<GrantRewardResult> DispatchDirectAsync(GrantRewardRequest request)
    {
        var bundle = request.Bundle;

        // Gold/Gems는 호출자가 Items에 ItemId=1/2로 포함하여 전달 — RewardBundle에 Gold/Gems 필드 없음
        // 동일 ItemId가 중복으로 들어올 경우 수량 합산 후 처리 (AddAsync 중복 키 오류 방지)
        var allItems = (bundle.Items ?? Enumerable.Empty<Domain.ValueObjects.RewardItem>())
            .GroupBy(i => i.ItemId)
            .Select(g => new Domain.ValueObjects.RewardItem(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        // 감사 로그 기록용 — 변동 전후 잔량 추적
        var auditEntries = new List<(int ItemId, int Delta, int Before, int After)>();

        foreach (var item in allItems)
        {
            var existing = await _itemRepo.GetByPlayerAndItemAsync(request.PlayerId, item.ItemId);
            var before = existing?.Quantity ?? 0;
            if (existing is not null)
                existing.Quantity += item.Quantity;
            else
                await _itemRepo.AddAsync(new PlayerItem
                {
                    PlayerId = request.PlayerId,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity
                });
            auditEntries.Add((item.ItemId, item.Quantity, before, before + item.Quantity));
        }

        // 아이템 변동 감사 로그 기록 — AuditLevel 필터링은 AuditLogService 내부에서 처리
        // Reason: "{sourcetype}:{sourcekey}" 형식으로 하드코딩 없이 호출 컨텍스트 식별
        foreach (var entry in auditEntries)
        {
            await _auditLogService.RecordAsync(
                request.PlayerId,
                entry.ItemId,
                reason: $"{request.SourceType.ToString().ToLower()}:{request.SourceKey}",
                entry.Delta,
                entry.Before,
                entry.After,
                request.ActorType,
                request.ActorId);
        }

        // ExpService 경유 — 임계값 초과 시 자동 레벨업 + 레벨업 보상 지급
        // [순환 호출 안전] 레벨업 보상(GrantLevelUpRewardAsync)은 Items만 포함(Exp=0)이므로
        //   ExpService.AddExpAsync 재진입 없이 반드시 종료됨
        // [트랜잭션 위치] DispatchDirectAsync는 ExecuteInTransactionAsync 내부에서 호출되므로
        //   아이템 지급과 Exp 처리가 동일 트랜잭션 스코프에 묶임
        if (bundle.Exp > 0)
        {
            await _expService.AddExpAsync(
                request.PlayerId,
                bundle.Exp,
                sourceKey: $"{request.SourceType}:{request.SourceKey}");
        }

        return GrantRewardResult.DirectSuccess();
    }

    // Mail 지급 — MailItems로 우편 생성
    // Currency-as-Item: Gold(ItemId=1) / Gems(ItemId=2)는 bundle.Items에 이미 포함되어 있어 별도 처리 불필요
    // Exp는 수령(ClaimAsync) 시점에 처리되므로 Mail.Exp에 저장
    // 트랜잭션 내부에서 호출되므로 SaveChangesAsync()는 중간 flush 역할 (커밋 아님)
    private async Task<GrantRewardResult> DispatchMailAsync(GrantRewardRequest request)
    {
        var bundle = request.Bundle;

        // 우편 엔티티 생성 — Gold/Gems는 bundle.Items에 ItemId=1/2로 포함되어 별도 처리 없음
        var mail = new Domain.Entities.Mail
        {
            PlayerId = request.PlayerId,
            Title = request.MailTitle,
            Body = request.MailBody,
            ItemId = null,
            ItemCount = 0,
            // Gold/Gems 필드는 Currency-as-Item 전환으로 제거됨 — Exp만 레거시 경로 유지
            Exp = bundle.Exp,
            ExpiresAt = DateTime.UtcNow.AddDays(request.MailExpiresInDays)
        };

        // 아이템 목록 첨부 — 동일 ItemId 중복 시 수량 합산 후 저장 (MailItems 테이블 중복 행 방지)
        if (bundle.Items is { Count: > 0 })
        {
            var groupedItems = bundle.Items
                .GroupBy(i => i.ItemId)
                .Select(g => new Domain.ValueObjects.RewardItem(g.Key, g.Sum(i => i.Quantity)));

            foreach (var item in groupedItems)
            {
                mail.MailItems.Add(new Domain.Entities.MailItem
                {
                    ItemId = item.ItemId,
                    Quantity = item.Quantity
                });
            }
        }

        await _mailRepo.AddAsync(mail);
        await _mailRepo.SaveChangesAsync();

        // GrantAsync에서 같은 트랜잭션 내 grant.MailId 업데이트가 이어지므로
        // 우편 생성 후 MailId 미연결 상태로 커밋되는 문제가 해결됨
        return GrantRewardResult.MailSuccess(mail.Id);
    }
}
