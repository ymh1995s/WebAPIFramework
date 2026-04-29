using Framework.Domain.Entities;
using Framework.Domain.Enums;
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
    public DbSet<PlayerItem> PlayerItems { get; set; }
    public DbSet<Mail> Mails { get; set; }
    public DbSet<MailItem> MailItems { get; set; }
    public DbSet<DailyLoginLog> DailyLoginLogs { get; set; }

    // 보상 프레임워크 테이블
    public DbSet<RewardGrant> RewardGrants { get; set; }
    public DbSet<RewardTable> RewardTables { get; set; }
    public DbSet<RewardTableEntry> RewardTableEntries { get; set; }

    // 게임 결과 테이블 (GameMatches → GameResults로 이름 변경)
    public DbSet<GameResult> GameResults { get; set; }
    public DbSet<GameResultParticipant> GameResultParticipants { get; set; }

    // 일일 보상 슬롯 테이블 (Current/Next 2슬롯 × Day 1~28 = 56행 고정)
    public DbSet<DailyRewardSlot> DailyRewardSlots { get; set; }

    // 공통 마스터 테이블
    public DbSet<Item> Items { get; set; }
    public DbSet<SystemConfig> SystemConfigs { get; set; }

    // 공지 테이블
    public DbSet<Notice> Notices { get; set; }

    // 소원수리함 테이블
    public DbSet<Inquiry> Inquiries { get; set; }

    // 운영 로그 테이블
    public DbSet<RateLimitLog> RateLimitLogs { get; set; }

    // 감사 로그 테이블 — 재화/아이템 변동 추적
    public DbSet<AuditLog> AuditLogs { get; set; }

    // 광고 보상 정책 테이블 — 광고 네트워크별 PlacementId → 보상 규칙 매핑
    public DbSet<AdPolicy> AdPolicies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SystemConfig: Key를 PK로 사용
        modelBuilder.Entity<SystemConfig>().HasKey(c => c.Key);

        // Player: AttendanceCount 기본값 0 — 신규 가입 플레이어는 출석 횟수 0에서 시작
        modelBuilder.Entity<Player>()
            .Property(p => p.AttendanceCount)
            .HasDefaultValue(0);

        // Player: IsBanned 기본값 false 설정 (DB 컬럼 DEFAULT 보장)
        modelBuilder.Entity<Player>()
            .Property(p => p.IsBanned)
            .HasDefaultValue(false);

        // Player: IsDeleted 기본값 false 설정
        modelBuilder.Entity<Player>()
            .Property(p => p.IsDeleted)
            .HasDefaultValue(false);

        // Global Query Filter — 소프트 딜리트된 계정은 일반 쿼리에서 자동 제외
        // Admin이나 특수 목적 조회는 IgnoreQueryFilters()로 우회
        modelBuilder.Entity<Player>().HasQueryFilter(p => !p.IsDeleted);

        // Player: PublicId 글로벌 Unique Index — 외부 공개 식별자 충돌 방지 (소프트 딜리트 포함 전체 유일)
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.PublicId)
            .IsUnique();

        // Player: DeviceId Partial Unique Index — 소프트 딜리트되지 않은 계정만 유니크 보장
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.DeviceId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // Player: GoogleId Partial Unique Index — 소프트 딜리트되지 않은 계정만 유니크 보장
        // 동일 GoogleId로 소프트 딜리트 계정이 있어도 신규 계정 생성 가능
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.GoogleId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // Player: MergedIntoPlayerId 자가참조 FK (ON DELETE SET NULL)
        // 병합 대상 계정이 탈퇴하면 MergedIntoPlayerId를 null로 초기화
        modelBuilder.Entity<Player>()
            .HasOne<Player>()
            .WithMany()
            .HasForeignKey(p => p.MergedIntoPlayerId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // MailItem → Mail (N:1) — 다중 아이템 우편 지원
        modelBuilder.Entity<MailItem>()
            .HasOne(mi => mi.Mail)
            .WithMany(m => m.MailItems)
            .HasForeignKey(mi => mi.MailId);

        // MailItem → Item (N:1)
        modelBuilder.Entity<MailItem>()
            .HasOne(mi => mi.Item)
            .WithMany()
            .HasForeignKey(mi => mi.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // RewardGrant → Player (N:1)
        modelBuilder.Entity<RewardGrant>()
            .HasOne(g => g.Player)
            .WithMany()
            .HasForeignKey(g => g.PlayerId);

        // RewardGrant → Mail (N:1, nullable) — 우편 지급 시에만 연결
        modelBuilder.Entity<RewardGrant>()
            .HasOne(g => g.Mail)
            .WithMany()
            .HasForeignKey(g => g.MailId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // RewardGrant: UNIQUE(PlayerId, SourceType, SourceKey) — 동일 보상 중복 지급 방지
        modelBuilder.Entity<RewardGrant>()
            .HasIndex(g => new { g.PlayerId, g.SourceType, g.SourceKey })
            .IsUnique();

        // RewardTable: IsDeleted 기본값 false
        modelBuilder.Entity<RewardTable>()
            .Property(t => t.IsDeleted)
            .HasDefaultValue(false);

        // RewardTable: UNIQUE(SourceType, Code)
        modelBuilder.Entity<RewardTable>()
            .HasIndex(t => new { t.SourceType, t.Code })
            .IsUnique();

        // RewardTableEntry → RewardTable (N:1)
        modelBuilder.Entity<RewardTableEntry>()
            .HasOne(e => e.RewardTable)
            .WithMany(t => t.Entries)
            .HasForeignKey(e => e.RewardTableId);

        // RewardTableEntry → Item (N:1)
        modelBuilder.Entity<RewardTableEntry>()
            .HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // GameResult: Guid PK
        modelBuilder.Entity<GameResult>()
            .HasKey(m => m.Id);

        // GameResultParticipant → GameResult (N:1)
        modelBuilder.Entity<GameResultParticipant>()
            .HasOne(p => p.Match)
            .WithMany(m => m.Participants)
            .HasForeignKey(p => p.MatchId);

        // GameResultParticipant → Player (N:1)
        modelBuilder.Entity<GameResultParticipant>()
            .HasOne(p => p.Player)
            .WithMany(pl => pl.MatchParticipants)
            .HasForeignKey(p => p.PlayerId);

        // GameResultParticipant: UNIQUE(MatchId, PlayerId) — 한 매치에 동일 플레이어 중복 방지
        modelBuilder.Entity<GameResultParticipant>()
            .HasIndex(p => new { p.MatchId, p.PlayerId })
            .IsUnique();

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

        // Mail: IsClaimed를 낙관적 동시성 토큰으로 설정
        //
        // [목적] 동일 우편에 대한 동시 Claim 요청이 2건 이상 들어와도 아이템이 단 1회만 지급되도록 강제
        //       — 애플리케이션 레벨 `if (mail.IsClaimed) return false` 체크만으로는
        //         두 요청이 거의 동시에 들어올 때 둘 다 통과하여 중복 수령이 발생할 수 있음
        //
        // [원리] EF Core가 Mail을 UPDATE할 때 엔티티를 읽어왔을 당시의 IsClaimed 값을 WHERE 절에 자동 포함시킴
        //   예) UPDATE Mails SET IsClaimed=TRUE WHERE Id=@id AND IsClaimed=FALSE
        //   → 이 row의 IsClaimed가 그 사이 다른 트랜잭션에 의해 바뀌었다면 매칭 실패 → 0 row 업데이트
        //
        // [동작 흐름 — 동일 mailId로 A, B 두 요청이 동시에 들어온 경우]
        //   1) A·B 모두 IsClaimed=false를 읽음 (애플리케이션 레벨 체크 통과)
        //   2) A가 먼저 SaveChanges → 1 row UPDATE 성공 → 아이템 지급 확정
        //   3) B가 SaveChanges → WHERE 조건 불일치로 0 row UPDATE
        //   4) EF Core가 DbUpdateConcurrencyException을 던짐 → 호출부에서 catch 후 false 반환
        //   5) B가 같은 SaveChanges에 묶어둔 아이템 수량 변경도 함께 롤백 → 중복 지급 차단
        //
        // [주의] 호출부(MailService.ClaimAsync)에서 IsClaimed 변경과 아이템 지급을 반드시
        //   동일한 DbContext + 단일 SaveChanges로 묶어야 위 롤백이 보장됨
        modelBuilder.Entity<Mail>()
            .Property(m => m.IsClaimed)
            .IsConcurrencyToken();

        // DailyLoginLog → Player (N:1), 플레이어+날짜 중복 방지
        modelBuilder.Entity<DailyLoginLog>()
            .HasOne(l => l.Player)
            .WithMany(p => p.LoginLogs)
            .HasForeignKey(l => l.PlayerId);

        modelBuilder.Entity<DailyLoginLog>()
            .HasIndex(l => new { l.PlayerId, l.LoginDate })
            .IsUnique();

        // DailyLoginLog: RewardDay 기본값 1 (기존 데이터 호환)
        modelBuilder.Entity<DailyLoginLog>()
            .Property(l => l.RewardDay)
            .HasDefaultValue(1);

        // DailyRewardSlot: 복합 PK (Slot, Day)
        modelBuilder.Entity<DailyRewardSlot>()
            .HasKey(s => new { s.Slot, s.Day });

        // DailyRewardSlot: ItemId nullable FK → Items (보상 없는 일자는 null)
        modelBuilder.Entity<DailyRewardSlot>()
            .HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // DailyRewardSlot: ItemCount 기본값 0
        modelBuilder.Entity<DailyRewardSlot>()
            .Property(s => s.ItemCount)
            .HasDefaultValue(0);

        // Inquiry → Player (N:1), 플레이어 삭제 시 문의도 함께 삭제
        modelBuilder.Entity<Inquiry>()
            .HasOne(i => i.Player)
            .WithMany(p => p.Inquiries)
            .HasForeignKey(i => i.PlayerId);

        // AuditLog: 플레이어별/시간순 조회 대비 인덱스
        modelBuilder.Entity<AuditLog>()
            .HasIndex(l => l.PlayerId);
        modelBuilder.Entity<AuditLog>()
            .HasIndex(l => l.CreatedAt);

        // AuditLog: ActorType 인덱스 — Admin 행위 빠른 필터링
        modelBuilder.Entity<AuditLog>()
            .HasIndex(l => l.ActorType);

        // RateLimitLog: PlayerId 부분 인덱스 — 인증 유저 요청만 추적 (null 제외)
        modelBuilder.Entity<RateLimitLog>()
            .HasIndex(l => l.PlayerId)
            .HasFilter("\"PlayerId\" IS NOT NULL");

        // AdPolicy: IsEnabled/IsDeleted 기본값
        modelBuilder.Entity<AdPolicy>()
            .Property(p => p.IsEnabled)
            .HasDefaultValue(true);

        modelBuilder.Entity<AdPolicy>()
            .Property(p => p.IsDeleted)
            .HasDefaultValue(false);

        // AdPolicy: DailyLimit 기본값 0 (무제한)
        modelBuilder.Entity<AdPolicy>()
            .Property(p => p.DailyLimit)
            .HasDefaultValue(0);

        // AdPolicy → RewardTable (N:1, nullable) — 보상 없는 정책도 허용
        modelBuilder.Entity<AdPolicy>()
            .HasOne(p => p.RewardTable)
            .WithMany()
            .HasForeignKey(p => p.RewardTableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // AdPolicy: UNIQUE(Network, PlacementId) WHERE !IsDeleted
        // 소프트 딜리트된 정책은 동일 PlacementId로 신규 정책 생성 가능
        modelBuilder.Entity<AdPolicy>()
            .HasIndex(p => new { p.Network, p.PlacementId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
