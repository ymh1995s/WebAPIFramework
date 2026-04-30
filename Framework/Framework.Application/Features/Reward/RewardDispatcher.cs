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
    private static DispatchMode DetermineMode(DispatchMode requested, RewardBundle bundle)
    {
        if (requested != DispatchMode.Auto) return requested;

        // 아이템이 있으면 Mail, 순수 Currency(Gold/Gems/Exp)만 있으면 Direct
        return bundle.IsCurrencyOnly ? DispatchMode.Direct : DispatchMode.Mail;
    }

    // Direct 지급 — PlayerProfile 컬럼 직접 증가
    // 트랜잭션 내부에서 호출되므로 SaveChangesAsync()는 중간 flush 역할 (커밋 아님)
    private async Task<GrantRewardResult> DispatchDirectAsync(GrantRewardRequest request)
    {
        var profile = await _profileRepo.GetByPlayerIdAsync(request.PlayerId)
            ?? throw new InvalidOperationException($"PlayerProfile을 찾을 수 없습니다. PlayerId: {request.PlayerId}");

        var bundle = request.Bundle;

        // Gold/Gems/Exp 직접 증가
        if (bundle.Gold > 0) profile.Gold += bundle.Gold;
        if (bundle.Gems > 0) profile.Gems += bundle.Gems;
        if (bundle.Exp > 0) profile.Exp += bundle.Exp;

        profile.UpdatedAt = DateTime.UtcNow;
        await _profileRepo.UpdateAsync(profile);

        // 아이템도 있는 경우 PlayerItem 인벤토리에 직접 추가
        // Profile 업데이트와 아이템 추가가 동일 트랜잭션 내 처리되어 부분 성공 불가
        if (bundle.Items is { Count: > 0 })
        {
            foreach (var item in bundle.Items)
            {
                var existing = await _itemRepo.GetByPlayerAndItemAsync(request.PlayerId, item.ItemId);
                if (existing is not null)
                    existing.Quantity += item.Quantity;
                else
                    await _itemRepo.AddAsync(new PlayerItem
                    {
                        PlayerId = request.PlayerId,
                        ItemId = item.ItemId,
                        Quantity = item.Quantity
                    });
            }
        }

        return GrantRewardResult.DirectSuccess();
    }

    // Mail 지급 — MailService를 통해 우편 + MailItems 생성
    // 트랜잭션 내부에서 호출되므로 SaveChangesAsync()는 중간 flush 역할 (커밋 아님)
    private async Task<GrantRewardResult> DispatchMailAsync(GrantRewardRequest request)
    {
        var bundle = request.Bundle;

        // 우편 엔티티 생성 (기존 단일 ItemId/ItemCount는 null/0으로 유지 — deprecated)
        var mail = new Domain.Entities.Mail
        {
            PlayerId = request.PlayerId,
            Title = request.MailTitle,
            Body = request.MailBody,
            ItemId = null,
            ItemCount = 0,
            ExpiresAt = DateTime.UtcNow.AddDays(request.MailExpiresInDays)
        };

        // MailItems 다중 아이템 첨부
        if (bundle.Items is { Count: > 0 })
        {
            foreach (var item in bundle.Items)
            {
                mail.MailItems.Add(new Domain.Entities.MailItem
                {
                    ItemId = item.ItemId,
                    Quantity = item.Quantity
                });
            }
        }

        // Gold/Gems/Exp는 우편 본문에 명시 (수령 시 직접 지급은 ClaimAsync에서 처리)
        // TODO: ClaimAsync에서 MailItems 기반 처리로 확장 시 Currency도 MailItem으로 표현 가능
        await _mailRepo.AddAsync(mail);
        await _mailRepo.SaveChangesAsync();

        // GrantAsync에서 같은 트랜잭션 내 grant.MailId 업데이트가 이어지므로
        // 우편 생성 후 MailId 미연결 상태로 커밋되는 문제가 해결됨
        return GrantRewardResult.MailSuccess(mail.Id);
    }
}
