using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Framework.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Repositories;

// 문의 저장소 구현체
public class InquiryRepository : IInquiryRepository
{
    private readonly AppDbContext _db;

    public InquiryRepository(AppDbContext db) => _db = db;

    // 특정 플레이어의 문의 목록 — 최신순 정렬
    public async Task<List<Inquiry>> GetByPlayerIdAsync(int playerId)
        => await _db.Inquiries
            .Where(i => i.PlayerId == playerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    // ID로 단건 조회
    public async Task<Inquiry?> GetByIdAsync(int id)
        => await _db.Inquiries.FirstOrDefaultAsync(i => i.Id == id);

    // 전체 문의 목록 — 플레이어 닉네임 포함, 최신순
    public async Task<List<Inquiry>> GetAllAsync()
        => await _db.Inquiries
            .Include(i => i.Player)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

    // 문의 추가
    public async Task AddAsync(Inquiry inquiry)
        => await _db.Inquiries.AddAsync(inquiry);

    // 변경사항 저장
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}
