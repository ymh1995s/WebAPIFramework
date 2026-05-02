using Framework.Application.Features.AuditLog;
using Framework.Application.Features.Exp;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Framework.Application.Features.Mail;

// 우편 서비스 구현체
public class MailService : IMailService
{
    private readonly IMailRepository _mailRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerItemRepository _playerItemRepository;
    private readonly IPlayerProfileRepository _playerProfileRepository;
    private readonly IExpService _expService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;

    public MailService(
        IMailRepository mailRepository,
        IPlayerRepository playerRepository,
        IPlayerItemRepository playerItemRepository,
        IPlayerProfileRepository playerProfileRepository,
        IExpService expService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork)
    {
        _mailRepository = mailRepository;
        _playerRepository = playerRepository;
        _playerItemRepository = playerItemRepository;
        _playerProfileRepository = playerProfileRepository;
        _expService = expService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
    }

    // 내 우편함 조회 - JWT에서 추출한 PlayerId 기준
    // MailItems 목록도 함께 반환 — 통화 아이템(Gold/Gems)은 MailItems에 포함됨
    public async Task<List<MailDto>> GetMyMailsAsync(int playerId)
    {
        var mails = await _mailRepository.GetByPlayerIdAsync(playerId);
        return mails.Select(m => new MailDto(
            m.Id, m.PlayerId, m.Title, m.Body,
            m.ItemId, m.Item?.Name, m.ItemCount,
            m.IsRead, m.IsClaimed, m.CreatedAt, m.ExpiresAt,
            m.Exp,
            // MailItems를 DTO로 변환 (없으면 null)
            m.MailItems.Count > 0
                ? m.MailItems.Select(mi => new MailItemDto(mi.ItemId, mi.Item?.Name ?? "", mi.Quantity)).ToList()
                : null
        )).ToList();
    }

    // 단일 플레이어에게 우편 발송 (Admin 전용)
    // Gold/Gems는 MailItems(통화 아이템)로 첨부하는 방식으로 전환 — Mail.Gold/Gems 레거시 필드 미사용
    public async Task SendAsync(SendMailDto dto)
    {
        var mail = new Domain.Entities.Mail
        {
            PlayerId = dto.PlayerId,
            Title = dto.Title,
            Body = dto.Body,
            ItemId = dto.ItemId,
            ItemCount = dto.ItemCount,
            // Gold/Gems 필드는 Currency-as-Item 전환으로 제거됨 — Exp만 레거시 경로 유지
            Exp = dto.Exp,
            ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays)
        };
        await _mailRepository.AddAsync(mail);
        await _mailRepository.SaveChangesAsync();
    }

    // 전체 플레이어에게 우편 일괄 발송 (Admin 전용)
    // Items 목록이 있으면 MailItems로 첨부 — 통화 아이템(Gold Id=1, Gems Id=2) 포함 가능
    public async Task BulkSendAsync(BulkSendMailDto dto)
    {
        var players = await _playerRepository.GetAllAsync();

        // 신규 Items 목록 기반 발송 여부 판단
        var hasMailItems = dto.Items is { Count: > 0 };

        var mails = players.Select(p =>
        {
            var mail = new Domain.Entities.Mail
            {
                PlayerId = p.Id,
                Title = dto.Title,
                Body = dto.Body,
                // 레거시 단일 아이템 필드 — Items 목록이 있으면 null 처리
                ItemId = hasMailItems ? null : dto.ItemId,
                ItemCount = hasMailItems ? 0 : dto.ItemCount,
                // Gold/Gems 필드는 Currency-as-Item 전환으로 제거됨 — Exp만 레거시 경로 유지
                Exp = dto.Exp,
                ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays)
            };

            // Items 목록이 있으면 MailItems로 첨부 (통화 아이템 포함)
            if (hasMailItems)
            {
                foreach (var item in dto.Items!)
                {
                    mail.MailItems.Add(new Domain.Entities.MailItem
                    {
                        ItemId = item.ItemId,
                        Quantity = item.Quantity
                    });
                }
            }

            return mail;
        }).ToList();

        await _mailRepository.AddRangeAsync(mails);
        await _mailRepository.SaveChangesAsync();
    }

    // 우편 수령 → 인벤토리에 아이템 추가 + 재화 지급
    // [보안] playerId로 본인 우편 여부 검증 — 타 유저 mailId로 남의 우편 조작 불가
    // [동시성] Mail.IsClaimed에 동시성 토큰이 걸려 있어, 동시 요청 중 한 쪽만 성공하고 나머지는 DbUpdateConcurrencyException
    // [원자성] 아이템 지급과 우편 상태 변경을 트랜잭션으로 묶어 부분 적용 방지
    // [MailItems 지원] 신규 다중 아이템 우편(MailItems)과 기존 단일 ItemId 우편 모두 처리
    public async Task<bool> ClaimAsync(int mailId, int playerId)
    {
        // MailItems 포함 조회 — 다중 아이템 우편 처리를 위해
        var mail = await _mailRepository.GetByIdWithItemsAsync(mailId);
        if (mail is null || mail.PlayerId != playerId || mail.IsClaimed) return false;

        // DbUpdateConcurrencyException은 트랜잭션 밖에서 처리 — 롤백 후 false 반환
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 우편 상태 전환 — 실제 DB 반영은 트랜잭션 커밋 시점
                mail.IsClaimed = true;
                mail.IsRead = true;

                var balanceBefore = 0;

                // [신규] MailItems 기반 다중 아이템 수령 처리 — 배치 조회로 N+1 방지
                // Gold/Gems는 MailItems(통화 아이템 ItemId=1/2)로 첨부되어 여기서 함께 처리됨
                // 레거시 Mail.Gold/Mail.Gems 경로는 기존 우편 호환을 위해 하단에 유지
                Dictionary<int, PlayerItem>? mailItemDict = null;
                if (mail.MailItems.Count > 0)
                {
                    // 배치 조회 — 모든 ItemId를 한 번의 IN 쿼리로 조회
                    var itemIds = mail.MailItems.Select(mi => mi.ItemId).ToList();
                    var existingItems = await _playerItemRepository.GetByPlayerAndItemIdsAsync(mail.PlayerId, itemIds);
                    mailItemDict = existingItems.ToDictionary(pi => pi.ItemId);

                    foreach (var mailItem in mail.MailItems)
                    {
                        if (mailItemDict.TryGetValue(mailItem.ItemId, out var existing))
                            existing.Quantity += mailItem.Quantity;
                        else
                        {
                            var newItem = new PlayerItem
                            {
                                PlayerId = mail.PlayerId,
                                ItemId = mailItem.ItemId,
                                Quantity = mailItem.Quantity
                            };
                            await _playerItemRepository.AddAsync(newItem);
                            // 딕셔너리에 추가 — 감사 로그 블록에서 재조회 없이 사용
                            mailItemDict[mailItem.ItemId] = newItem;
                        }
                    }
                }
                // [기존 호환] ItemId 기반 단일 아이템 수령 처리 (deprecated — 기존 우편 호환용)
                else if (mail.ItemId.HasValue)
                {
                    var existing = await _playerItemRepository.GetByPlayerAndItemAsync(mail.PlayerId, mail.ItemId.Value);
                    balanceBefore = existing?.Quantity ?? 0;
                    if (existing is not null)
                        existing.Quantity += mail.ItemCount;
                    else
                        await _playerItemRepository.AddAsync(new PlayerItem
                        {
                            PlayerId = mail.PlayerId,
                            ItemId = mail.ItemId.Value,
                            Quantity = mail.ItemCount
                        });
                }

                // 감사 로그는 수령 확정 후 별도로 기록 (AuditLevel에 따라 내부에서 저장 여부 결정)
                // [MailItems 경로] 메모리 내 딕셔너리 사용 — SaveChanges 후 재조회 없이 수량 계산
                if (mail.MailItems.Count > 0 && mailItemDict is not null)
                {
                    foreach (var mailItem in mail.MailItems)
                    {
                        var currentQty = mailItemDict[mailItem.ItemId].Quantity;
                        // 수령 전 잔고 = 현재값 - 지급량
                        var beforeQty = currentQty - mailItem.Quantity;

                        await _auditLogService.RecordAsync(
                            mail.PlayerId, mailItem.ItemId, "MailClaim",
                            mailItem.Quantity, beforeQty, currentQty);
                    }
                }
                // [기존 경로] 단일 ItemId 우편 감사 로그 (deprecated — 기존 우편 호환)
                else if (mail.ItemId.HasValue)
                {
                    await _auditLogService.RecordAsync(
                        mail.PlayerId, mail.ItemId.Value, "MailClaim",
                        mail.ItemCount, balanceBefore, balanceBefore + mail.ItemCount);
                }

                // Exp는 레벨업 처리가 포함되어 있으므로 별도로 처리
                // TODO: 감사 로그 구조 개선 시 Currency(Gold/Gems) 로그도 기록 필요
                if (mail.Exp > 0)
                    await _expService.AddExpAsync(mail.PlayerId, mail.Exp, $"mail:{mail.Id}");

                return true;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            // 동시 요청으로 다른 쪽이 먼저 수령 처리한 경우 — 트랜잭션 롤백 후 false 반환
            return false;
        }
    }

    // 다수 Mail 엔티티를 컨텍스트에 추가 (배치 발송 시 — 호출부에서 SaveChanges 필요)
    public async Task AddRangeMailsAsync(IEnumerable<Domain.Entities.Mail> mails)
    {
        await _mailRepository.AddRangeAsync(mails);
    }
}
