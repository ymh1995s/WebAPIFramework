using Framework.Domain.Content.Entities;
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

    // 1회 공지 테이블 — Admin이 전체 또는 특정 플레이어에게 발송하는 HUD 메시지
    public DbSet<Shout> Shouts => Set<Shout>();

    // 소원수리함 테이블
    public DbSet<Inquiry> Inquiries { get; set; }

    // 운영 로그 테이블
    public DbSet<RateLimitLog> RateLimitLogs { get; set; }

    // 감사 로그 테이블 — 재화/아이템 변동 추적
    public DbSet<AuditLog> AuditLogs { get; set; }

    // 광고 보상 정책 테이블 — 광고 네트워크별 PlacementId → 보상 규칙 매핑
    public DbSet<AdPolicy> AdPolicies { get; set; }

    // 인앱결제 테이블 — 상품 마스터 및 구매 이력
    public DbSet<IapProduct> IapProducts { get; set; }
    public DbSet<IapPurchase> IapPurchases { get; set; }

    // Admin 운영 알림 테이블 — IAP 환불, 어뷰징 감지 등 운영 이슈 알림
    public DbSet<AdminNotification> AdminNotifications { get; set; }

    // 밴/밴해제 감사 이력 테이블 — Admin 처리 이력 영구 보존
    public DbSet<BanLog> BanLogs { get; set; }

    // 컨텐츠 영역 — 스테이지 마스터 및 클리어 기록
    public DbSet<Stage> Stages { get; set; }
    public DbSet<StageClear> StageClears { get; set; }

    // 레벨 임계값 마스터 테이블 — 레벨별 누적 경험치 기준 (DB 외부화)
    public DbSet<LevelThreshold> LevelThresholds { get; set; }

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

        // PlayerItem: UNIQUE(PlayerId, ItemId) — 동일 플레이어+아이템 조합은 수량만 증가
        // Currency-as-Item 방식으로 Gold/Gems도 PlayerItem에서 관리하므로 유니크 제약 필수
        modelBuilder.Entity<PlayerItem>()
            .HasIndex(pi => new { pi.PlayerId, pi.ItemId })
            .IsUnique();

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

        // Inquiry 컬럼 길이 제약 — 상수 참조로 DTO/Entity/DB 간 일관성 보장
        modelBuilder.Entity<Inquiry>()
            .Property(i => i.Content)
            .HasMaxLength(Inquiry.ContentMaxLength);
        modelBuilder.Entity<Inquiry>()
            .Property(i => i.AdminReply)
            .HasMaxLength(Inquiry.AdminReplyMaxLength);

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

        // IapProduct: IsEnabled/IsDeleted 기본값
        modelBuilder.Entity<IapProduct>()
            .Property(p => p.IsEnabled)
            .HasDefaultValue(true);

        modelBuilder.Entity<IapProduct>()
            .Property(p => p.IsDeleted)
            .HasDefaultValue(false);

        // IapProduct: 컬럼 최대 길이 설정
        modelBuilder.Entity<IapProduct>()
            .Property(p => p.ProductId)
            .HasMaxLength(128);

        modelBuilder.Entity<IapProduct>()
            .Property(p => p.Description)
            .HasMaxLength(512);

        // IapProduct → RewardTable (N:1, nullable) — 보상 없는 상품도 허용
        modelBuilder.Entity<IapProduct>()
            .HasOne(p => p.RewardTable)
            .WithMany()
            .HasForeignKey(p => p.RewardTableId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // IapProduct: UNIQUE(Store, ProductId) WHERE IsDeleted=false
        // 소프트 딜리트된 상품은 동일 SKU로 신규 상품 생성 가능
        modelBuilder.Entity<IapProduct>()
            .HasIndex(p => new { p.Store, p.ProductId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // IapPurchase: 컬럼 최대 길이 설정
        modelBuilder.Entity<IapPurchase>()
            .Property(p => p.ProductId)
            .HasMaxLength(128);

        modelBuilder.Entity<IapPurchase>()
            .Property(p => p.PurchaseToken)
            .HasMaxLength(512);

        modelBuilder.Entity<IapPurchase>()
            .Property(p => p.OrderId)
            .HasMaxLength(128);

        modelBuilder.Entity<IapPurchase>()
            .Property(p => p.FailureReason)
            .HasMaxLength(256);

        modelBuilder.Entity<IapPurchase>()
            .Property(p => p.ClientIp)
            .HasMaxLength(64);

        // IapPurchase → Player (N:1, Restrict) — 플레이어 삭제 시 구매 이력 보존
        modelBuilder.Entity<IapPurchase>()
            .HasOne(p => p.Player)
            .WithMany()
            .HasForeignKey(p => p.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // IapPurchase: UNIQUE(Store, PurchaseToken) — 동일 결제 토큰 중복 처리 방지
        modelBuilder.Entity<IapPurchase>()
            .HasIndex(p => new { p.Store, p.PurchaseToken })
            .IsUnique();

        // IapPurchase: INDEX(PlayerId, CreatedAt desc) — 플레이어별 구매 이력 조회 최적화
        modelBuilder.Entity<IapPurchase>()
            .HasIndex(p => new { p.PlayerId, p.CreatedAt });

        // ─────────────────────────────────────────────
        // 컨텐츠 영역 — 스테이지 / 스테이지 클리어
        // ─────────────────────────────────────────────

        // Stage: Code UNIQUE 인덱스 — 클라이언트 참조 식별자
        modelBuilder.Entity<Stage>()
            .HasIndex(s => s.Code)
            .IsUnique();

        // Stage: IsActive 기본값 true
        modelBuilder.Entity<Stage>()
            .Property(s => s.IsActive)
            .HasDefaultValue(true);

        // Stage: RePlayRewardDecayPercent 기본값 0
        modelBuilder.Entity<Stage>()
            .Property(s => s.RePlayRewardDecayPercent)
            .HasDefaultValue(0);

        // Stage: ExpReward 기본값 0
        modelBuilder.Entity<Stage>()
            .Property(s => s.ExpReward)
            .HasDefaultValue(0);

        // Stage: SortOrder 기본값 0
        modelBuilder.Entity<Stage>()
            .Property(s => s.SortOrder)
            .HasDefaultValue(0);

        // Stage: RequiredPrevStageId 자기 참조 FK (ON DELETE SET NULL)
        // 선행 스테이지 삭제 시 조건을 null로 초기화
        modelBuilder.Entity<Stage>()
            .HasOne<Stage>()
            .WithMany()
            .HasForeignKey(s => s.RequiredPrevStageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // StageClear: UNIQUE(PlayerId, StageId) — 동일 스테이지 중복 기록 방지
        modelBuilder.Entity<StageClear>()
            .HasIndex(c => new { c.PlayerId, c.StageId })
            .IsUnique();

        // StageClear: ClearCount 기본값 1
        modelBuilder.Entity<StageClear>()
            .Property(c => c.ClearCount)
            .HasDefaultValue(1);

        // StageClear: PlayerId 인덱스 — 플레이어별 진행 현황 조회 최적화
        modelBuilder.Entity<StageClear>()
            .HasIndex(c => c.PlayerId);

        // ─────────────────────────────────────────────
        // 1회 공지 — 전체/특정 플레이어 대상 HUD 메시지
        // ─────────────────────────────────────────────

        // Shout → Player (N:1, nullable) — 플레이어 삭제 시 1회 공지도 함께 삭제
        modelBuilder.Entity<Shout>(entity =>
        {
            entity.HasOne(s => s.Player)
                  .WithMany()
                  .HasForeignKey(s => s.PlayerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);

            // 플레이어별 활성 1회 공지 조회 최적화
            entity.HasIndex(s => s.PlayerId);

            // 만료 1회 공지 필터링 최적화
            entity.HasIndex(s => s.ExpiresAt);
        });

        // ─────────────────────────────────────────────
        // 레벨 임계값 마스터 — Level을 PK로, 초기 시드 데이터 설정
        // ─────────────────────────────────────────────

        // AdminNotification 인덱스 설정
        modelBuilder.Entity<AdminNotification>(entity =>
        {
            // DedupKey 부분 UNIQUE — null 아닌 경우만 중복 방지
            entity.HasIndex(n => n.DedupKey)
                .IsUnique()
                .HasFilter("\"DedupKey\" IS NOT NULL");
            // 미확인 알림 최신순 조회
            entity.HasIndex(n => new { n.IsRead, n.CreatedAt });
            // 분류별 조회
            entity.HasIndex(n => new { n.Category, n.CreatedAt });
        });

        // ─────────────────────────────────────────────
        // BanLog — 밴/밴해제 감사 이력
        // ─────────────────────────────────────────────

        // BanLog: 플레이어별+최신순 조회 인덱스 (가장 빈번한 쿼리)
        modelBuilder.Entity<BanLog>()
            .HasIndex(b => new { b.PlayerId, b.CreatedAt });

        // BanLog: 전체 타임라인 최신순 조회 인덱스
        modelBuilder.Entity<BanLog>()
            .HasIndex(b => b.CreatedAt);

        // BanLog: Reason 길이 제한 (500자)
        modelBuilder.Entity<BanLog>()
            .Property(b => b.Reason)
            .HasMaxLength(500);

        // BanLog: ActorIp 길이 제한 (IPv6 포함 최대 45자)
        modelBuilder.Entity<BanLog>()
            .Property(b => b.ActorIp)
            .HasMaxLength(45);

        // LevelThreshold: Level을 PK로 사용
        modelBuilder.Entity<LevelThreshold>().HasKey(t => t.Level);

        // LevelThreshold: 초기 시드 데이터 (Level 1~20)
        // UpdatedAt은 마이그레이션 시점 고정값으로 설정 (DateTime.UtcNow 사용 불가 — EF 제약)
        var seedDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<LevelThreshold>().HasData(
            new LevelThreshold { Level = 1,  RequiredExp = 0,     UpdatedAt = seedDate },
            new LevelThreshold { Level = 2,  RequiredExp = 100,   UpdatedAt = seedDate },
            new LevelThreshold { Level = 3,  RequiredExp = 250,   UpdatedAt = seedDate },
            new LevelThreshold { Level = 4,  RequiredExp = 450,   UpdatedAt = seedDate },
            new LevelThreshold { Level = 5,  RequiredExp = 700,   UpdatedAt = seedDate },
            new LevelThreshold { Level = 6,  RequiredExp = 1000,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 7,  RequiredExp = 1400,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 8,  RequiredExp = 1900,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 9,  RequiredExp = 2500,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 10, RequiredExp = 3200,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 11, RequiredExp = 4000,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 12, RequiredExp = 5000,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 13, RequiredExp = 6200,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 14, RequiredExp = 7600,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 15, RequiredExp = 9200,  UpdatedAt = seedDate },
            new LevelThreshold { Level = 16, RequiredExp = 11000, UpdatedAt = seedDate },
            new LevelThreshold { Level = 17, RequiredExp = 13000, UpdatedAt = seedDate },
            new LevelThreshold { Level = 18, RequiredExp = 15500, UpdatedAt = seedDate },
            new LevelThreshold { Level = 19, RequiredExp = 18500, UpdatedAt = seedDate },
            new LevelThreshold { Level = 20, RequiredExp = 22000, UpdatedAt = seedDate }
        );
    }
}
