using Framework.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Framework.Infrastructure.Persistence;

// EF Core DB 컨텍스트 - PostgreSQL 연결 및 테이블 매핑
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // 인증 관련 테이블
    public DbSet<Player> Players { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // 인게임 데이터 테이블
    public DbSet<PlayerProfile> PlayerProfiles { get; set; }
    public DbSet<PlayerRecord> PlayerRecords { get; set; }
    public DbSet<PlayerItem> PlayerItems { get; set; }
    public DbSet<Mail> Mails { get; set; }
    public DbSet<DailyLoginLog> DailyLoginLogs { get; set; }
    public DbSet<DailyRewardConfig> DailyRewardConfigs { get; set; }

    // 공통 마스터 테이블
    public DbSet<Item> Items { get; set; }
    public DbSet<SystemConfig> SystemConfigs { get; set; }

    // 공지 테이블
    public DbSet<Notice> Notices { get; set; }

    // 운영 로그 테이블
    public DbSet<RateLimitLog> RateLimitLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SystemConfig: Key를 PK로 사용
        modelBuilder.Entity<SystemConfig>().HasKey(c => c.Key);

        // Player: DeviceId 유니크 인덱스
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.DeviceId)
            .IsUnique();

        // Player: GoogleId 유니크 인덱스 (null 허용)
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.GoogleId)
            .IsUnique();

        // RefreshToken: Token 유니크 인덱스
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();

        // RefreshToken → Player (N:1)
        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.Player)
            .WithMany(p => p.RefreshTokens)
            .HasForeignKey(r => r.PlayerId);

        // PlayerProfile → Player (1:1)
        modelBuilder.Entity<PlayerProfile>()
            .HasOne(pp => pp.Player)
            .WithOne(p => p.Profile)
            .HasForeignKey<PlayerProfile>(pp => pp.PlayerId);

        // PlayerRecord → Player (N:1)
        modelBuilder.Entity<PlayerRecord>()
            .HasOne(r => r.Player)
            .WithMany(p => p.Records)
            .HasForeignKey(r => r.PlayerId);

        // PlayerItem → Player (N:1)
        modelBuilder.Entity<PlayerItem>()
            .HasOne(pi => pi.Player)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PlayerId);

        // Mail → Player (N:1)
        modelBuilder.Entity<Mail>()
            .HasOne(m => m.Player)
            .WithMany(p => p.Mails)
            .HasForeignKey(m => m.PlayerId);

        // DailyLoginLog → Player (N:1), 플레이어+날짜 중복 방지
        modelBuilder.Entity<DailyLoginLog>()
            .HasOne(l => l.Player)
            .WithMany(p => p.LoginLogs)
            .HasForeignKey(l => l.PlayerId);

        modelBuilder.Entity<DailyLoginLog>()
            .HasIndex(l => new { l.PlayerId, l.LoginDate })
            .IsUnique();
    }
}
