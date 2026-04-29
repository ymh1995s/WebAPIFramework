using Framework.Application.Features.AuditLog;
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
    private readonly IAuditLogService _auditLogService;

    public MailService(
        IMailRepository mailRepository,
        IPlayerRepository playerRepository,
        IPlayerItemRepository playerItemRepository,
        IAuditLogService auditLogService)
    {
        _mailRepository = mailRepository;
        _playerRepository = playerRepository;
        _playerItemRepository = playerItemRepository;
        _auditLogService = auditLogService;
    }

    // 내 우편함 조회 - JWT에서 추출한 PlayerId 기준
    public async Task<List<MailDto>> GetMyMailsAsync(int playerId)
    {
        var mails = await _mailRepository.GetByPlayerIdAsync(playerId);
        return mails.Select(m => new MailDto(
            m.Id, m.PlayerId, m.Title, m.Body,
            m.ItemId, m.Item?.Name, m.ItemCount,
            m.IsRead, m.IsClaimed, m.CreatedAt, m.ExpiresAt
        )).ToList();
    }

    // 단일 플레이어에게 우편 발송 (Admin 전용)
    public async Task SendAsync(SendMailDto dto)
    {
        var mail = new Domain.Entities.Mail
        {
            PlayerId = dto.PlayerId,
            Title = dto.Title,
            Body = dto.Body,
            ItemId = dto.ItemId,
            ItemCount = dto.ItemCount,
            ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays)
        };
        await _mailRepository.AddAsync(mail);
        await _mailRepository.SaveChangesAsync();
    }

    // 전체 플레이어에게 우편 일괄 발송 (Admin 전용)
    public async Task BulkSendAsync(BulkSendMailDto dto)
    {
        var players = await _playerRepository.GetAllAsync();
        var mails = players.Select(p => new Domain.Entities.Mail
        {
            PlayerId = p.Id,
            Title = dto.Title,
            Body = dto.Body,
            ItemId = dto.ItemId,
            ItemCount = dto.ItemCount,
            ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays)
        });
        await _mailRepository.AddRangeAsync(mails);
        await _mailRepository.SaveChangesAsync();
    }

    // 우편 수령 → 인벤토리에 아이템 추가
    // [보안] playerId로 본인 우편 여부 검증 — 타 유저 mailId로 남의 우편 조작 불가
    // [동시성] Mail.IsClaimed에 동시성 토큰이 걸려 있어, 동시 요청 중 한 쪽만 성공하고 나머지는 DbUpdateConcurrencyException
    // [원자성] 아이템 지급과 우편 상태 변경을 단일 SaveChanges로 묶어 부분 적용 방지
    // [MailItems 지원] 신규 다중 아이템 우편(MailItems)과 기존 단일 ItemId 우편 모두 처리
    public async Task<bool> ClaimAsync(int mailId, int playerId)
    {
        // MailItems 포함 조회 — 다중 아이템 우편 처리를 위해
        var mail = await _mailRepository.GetByIdWithItemsAsync(mailId);
        if (mail is null || mail.PlayerId != playerId || mail.IsClaimed) return false;

        // 우편 상태 전환 — 실제 DB 반영은 마지막 SaveChanges 시점
        mail.IsClaimed = true;
        mail.IsRead = true;

        var balanceBefore = 0;

        // [신규] MailItems 기반 다중 아이템 수령 처리
        if (mail.MailItems.Count > 0)
        {
            foreach (var mailItem in mail.MailItems)
            {
                var existing = await _playerItemRepository.GetByPlayerAndItemAsync(mail.PlayerId, mailItem.ItemId);
                if (existing is not null)
                    existing.Quantity += mailItem.Quantity;
                else
                    await _playerItemRepository.AddAsync(new PlayerItem
                    {
                        PlayerId = mail.PlayerId,
                        ItemId = mailItem.ItemId,
                        Quantity = mailItem.Quantity
                    });
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

        // 모든 변경을 한 번의 SaveChanges로 커밋 — 동일 DbContext를 공유하므로 원자적으로 반영됨
        try
        {
            await _mailRepository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 동시 요청으로 다른 쪽이 먼저 수령 처리한 경우 — 여기서는 아이템 변경도 함께 롤백됨
            return false;
        }

        // 감사 로그는 수령 확정 후 별도로 기록 (AuditLevel에 따라 내부에서 저장 여부 결정)
        // [MailItems 경로] 다중 아이템 우편 — 각 아이템별로 감사 로그 기록
        if (mail.MailItems.Count > 0)
        {
            foreach (var mailItem in mail.MailItems)
            {
                // 수령 후 현재 보유량 조회 (위에서 이미 Quantity 증가 후 SaveChanges 완료된 상태)
                var existingItem = await _playerItemRepository.GetByPlayerAndItemAsync(mail.PlayerId, mailItem.ItemId);
                var currentQty = existingItem?.Quantity ?? 0;
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

        return true;
    }

    // 다수 Mail 엔티티를 컨텍스트에 추가 (배치 발송 시 — 호출부에서 SaveChanges 필요)
    public async Task AddRangeMailsAsync(IEnumerable<Domain.Entities.Mail> mails)
    {
        await _mailRepository.AddRangeAsync(mails);
    }
}
