using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 공지 저장소 구현체
public class NoticeRepository : INoticeRepository
{
    private readonly AppDbContext _context;

    public NoticeRepository(AppDbContext context)
    {
        _context = context;
    }

    // 클라이언트용 — 활성 공지 중 가장 최근 1개
    public async Task<Notice?> GetLatestActiveAsync()
        => await _context.Notices.Where(n => n.IsActive).OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync();

    // Admin용 — 전체 최신순 조회
    public async Task<List<Notice>> GetAllAsync()
        => await _context.Notices.OrderByDescending(n => n.CreatedAt).ToListAsync();

    public async Task<Notice?> GetByIdAsync(int id)
        => await _context.Notices.FindAsync(id);

    public async Task AddAsync(Notice notice)
        => await _context.Notices.AddAsync(notice);

    public void Delete(Notice notice)
        => _context.Notices.Remove(notice);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
