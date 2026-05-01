using System.Text.Json;
using Framework.Application.Features.AuditLog;
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
public class RewardDispatcher : IRewardDispatcher
{
    private readonly IRewardGrantRepository _grantRepo;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerProfileRepository _profileRepo;
    private readonly IPlayerItemRepository _itemRepo;
    private readonly IMailRepository _mailRepo;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RewardDispatcher> _logger;

    public RewardDispatcher(
        IRewardGrantRepository grantRepo,
        IPlayerRepository playerRepository,
        IPlayerProfileRepository profileRepo,
        IPlayerItemRepository itemRepo,
        IMailRepository mailRepo,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<RewardDispatcher> logger)
    {
        _grantRepo = grantRepo;
        _playerRepository = playerRepository;
        _profileRepo = profileRepo;
        _itemRepo = itemRepo;
        _mailRepo = mailRepo;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 보상 지급 — 공통 파이프라인 진입점
    // [순서] 번들 검증 → PlayerId 확인 → 트랜잭션 스코프 → 선기록 → 지급 → MailId 업데이트
    // [원자성] ExecuteInTransactionAsync로 전체 흐름을 단일 트랜잭션 스코프로 묶음
    // [중첩 트랜잭션 지원] 외부 트랜잭션 활성 시 자동으로 참여자가 되어 커밋/롤백을 호출자에게 위임
    public async Task<GrantRewardResult> GrantAsync(GrantRewardRequest request)
    {
        // 빈 번들이면 지급할 내용 없음
        if (request.Bundle.IsEmpty)
            return GrantRewardResult.Fail("지급할 보상이 없습니다.");

        // PlayerId 존재 여부 확인 — FK 위반 방지 (트랜잭션 시작 전 검증)
        var player = await _playerRepository.GetByIdAsync(request.PlayerId);
        if (player is null)
            return GrantRewardResult.NotFound();

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
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
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

    // UNIQUE 제약 위반 여부 확인 — PostgreSQL SqlState 23505
    // Application 레이어가 Npgsql에 직접 의존하지 않도록 InnerException 메시지로 판별
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
           || ex.InnerException?.GetType().Name == "PostgresException" &&
              (ex.InnerException?.Message.Contains("unique") == true ||
               ex.InnerException?.Message.Contains("duplicate") == true);

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

        // Exp는 PlayerProfile에 직접 증가 (레벨업 처리는 ExpService에서 담당)
        // 보상 파이프라인에서 Exp는 별도 GrantAsync 완료 후 처리하므로 여기서는 Profile만 업데이트
        if (bundle.Exp > 0)
        {
            var profile = await _profileRepo.GetByPlayerIdAsync(request.PlayerId)
                ?? throw new InvalidOperationException($"PlayerProfile을 찾을 수 없습니다. PlayerId: {request.PlayerId}");
            profile.Exp += bundle.Exp;
            profile.UpdatedAt = DateTime.UtcNow;
            await _profileRepo.UpdateAsync(profile);
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
