using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 우편 저장소 구현체
public class MailRepository : IMailRepository
{
    private readonly AppDbContext _context;

    public MailRepository(AppDbContext context)
    {
        _context = context;
    }

    // 특정 플레이어의 전체 우편 조회
    // Item(단일 레거시) + MailItems(다중 아이템, 통화 포함) 함께 로드
    public async Task<List<Mail>> GetByPlayerIdAsync(int playerId)
        => await _context.Mails
            .Include(m => m.Item)
            .Include(m => m.MailItems)
                .ThenInclude(mi => mi.Item)
            .Where(m => m.PlayerId == playerId)
            .ToListAsync();

    // ID로 단건 조회
    public async Task<Mail?> GetByIdAsync(int id)
        => await _context.Mails.FindAsync(id);

    // ID로 단건 조회 (MailItems 포함 — 다중 아이템 우편 수령 시 사용)
    public async Task<Mail?> GetByIdWithItemsAsync(int id)
        => await _context.Mails
            .Include(m => m.MailItems)
            .FirstOrDefaultAsync(m => m.Id == id);

    // 단건 우편 추가
    public async Task AddAsync(Mail mail)
        => await _context.Mails.AddAsync(mail);

    // 다수 우편 일괄 추가
    public async Task AddRangeAsync(IEnumerable<Mail> mails)
        => await _context.Mails.AddRangeAsync(mails);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
