using Framework.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Persistence;

// EF Core DB 컨텍스트 - PostgreSQL 연결 및 테이블 매핑
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 플레이어 기록 테이블
    public DbSet<PlayerRecord> PlayerRecords { get; set; }
}
