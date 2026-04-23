using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Framework.Application.Services;

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
        var mail = new Mail
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
        var mails = players.Select(p => new Mail
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
    public async Task<bool> ClaimAsync(int mailId, int playerId)
    {
        var mail = await _mailRepository.GetByIdAsync(mailId);
        if (mail is null || mail.PlayerId != playerId || mail.IsClaimed) return false;

        // 우편 상태 전환 — 실제 DB 반영은 마지막 SaveChanges 시점
        mail.IsClaimed = true;
        mail.IsRead = true;

        var balanceBefore = 0;
        if (mail.ItemId.HasValue)
        {
            // 기존 보유 아이템이면 수량 증가, 없으면 신규 추가
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
        if (mail.ItemId.HasValue)
        {
            await _auditLogService.RecordAsync(
                mail.PlayerId, mail.ItemId.Value, "MailClaim",
                mail.ItemCount, balanceBefore, balanceBefore + mail.ItemCount);
        }

        return true;
    }
}
