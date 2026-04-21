using Framework.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Persistence;

// EF Core DB 컨텍스트 - PostgreSQL 연결 및 테이블 매핑
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 플레이어 기록 테이블
    public DbSet<PlayerRecord> PlayerRecords { get; set; }
    // 아이템 마스터 테이블
    public DbSet<Item> Items { get; set; }
    // 플레이어 인벤토리 테이블
    public DbSet<PlayerItem> PlayerItems { get; set; }
    // 우편 테이블
    public DbSet<Mail> Mails { get; set; }
    // 일일 로그인 기록 테이블
    public DbSet<DailyLoginLog> DailyLoginLogs { get; set; }
    // 일일 보상 설정 테이블
    public DbSet<DailyRewardConfig> DailyRewardConfigs { get; set; }
    // 시스템 설정 테이블
    public DbSet<SystemConfig> SystemConfigs { get; set; }

    // EF Core가 DB 스키마를 생성할 때 호출되는 훅 - PK/인덱스/관계 등 FluentAPI 설정을 여기서 정의
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SystemConfig: Key를 PK로 사용
        modelBuilder.Entity<SystemConfig>().HasKey(c => c.Key);

        // DailyLoginLog: 플레이어+날짜 조합 중복 방지
        modelBuilder.Entity<DailyLoginLog>()
            .HasIndex(l => new { l.PlayerId, l.LoginDate })
            .IsUnique();
    }
}
