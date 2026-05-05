using Framework.Domain.Constants;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Framework.Application.BackgroundServices;

// PII 자동 정리 백그라운드 서비스 — KST 매일 DailyRunHourKst 시각 1회 실행
// [정리 대상]
//   AuditLog      : 보관 기간 초과 행 hard delete
//   RateLimitLog  : 보관 기간 초과 행 hard delete
//   BanLog        : 보관 기간 초과 ActorIp NULL 익명화 (행 자체는 영구 보존)
//   IapPurchase   : 상태 종결(Granted/Refunded/Failed) + 보관 기간 초과 ClientIp NULL 익명화
// [단일 인스턴스 가정] advisory lock 미적용 — 복수 인스턴스 환경에서는 advisory lock 도입 필요
public class PiiRetentionCleanupService : BackgroundService
{
    // BackgroundService는 Singleton 수명 — Scoped 서비스(DB 등) 사용 시 IServiceScopeFactory 필수
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PiiRetentionOptions> _options;
    private readonly ILogger<PiiRetentionCleanupService> _logger;

    public PiiRetentionCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<PiiRetentionOptions> options,
        ILogger<PiiRetentionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    // 백그라운드 실행 진입점 — 시작 딜레이 후 KST 기준 매일 1회 정리 반복
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 서버 시작 직후 즉시 실행 방지 — 다른 Scoped 서비스 초기화 완료 대기
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // 다음 KST DailyRunHourKst:00까지 대기
            var delay = CalculateNextRunDelay(_options.Value.DailyRunHourKst);
            _logger.LogInformation(
                "PiiRetentionCleanup 다음 실행 예정 — {Delay:hh\\:mm\\:ss} 후 (KST {Hour}:00)",
                delay, _options.Value.DailyRunHourKst);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 서비스 종료 신호 — 정상 종료
                break;
            }

            // 취소 신호 도착 시 정리 작업 건너뜀
            if (stoppingToken.IsCancellationRequested) break;

            // 정리 실행 — 예외 throw 금지, 로그만 기록 후 다음 주기로 넘어감
            await RunCleanupAsync(stoppingToken);
        }
    }

    // 다음 KST targetHour:00 까지의 대기 시간 계산
    // 현재 KST 시각이 targetHour 이후이면 다음 날 같은 시각을 목표로 설정
    private static TimeSpan CalculateNextRunDelay(int targetHourKst)
    {
        // 현재 UTC → KST 변환
        var nowKst = DateTime.UtcNow.Add(TimeConstants.KstOffset);
        var targetToday = nowKst.Date.AddHours(targetHourKst);

        // 목표 시각이 이미 지났으면 내일 같은 시각을 목표로 설정
        var target = nowKst < targetToday ? targetToday : targetToday.AddDays(1);

        // KST 기준 대기 시간 → UTC 기준으로 변환 필요 없이 TimeSpan 차이만 사용
        var delay = target - nowKst;

        // 최소 딜레이 보장 (음수 방지 — 시스템 시계 오차 대비)
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(10);
    }

    // 실제 정리 실행 메서드 — 4개 단계 순차 처리
    // 전체를 try/catch로 감싸 예외가 서비스 루프를 중단시키지 않도록 보호
    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var opts = _options.Value;

        // 비상 정지 스위치 확인
        if (!opts.Enabled)
        {
            _logger.LogInformation("PiiRetentionCleanup 건너뜀 — Enabled=false");
            return;
        }

        try
        {
            _logger.LogInformation("PiiRetentionCleanup 시작 — {UtcNow:u}", DateTime.UtcNow);
            var totalStart = DateTime.UtcNow;

            // 매 정리 실행마다 새 스코프 생성 — DbContext 수명 격리
            using var scope = _scopeFactory.CreateScope();

            // DbContext를 추상 타입으로 resolve — 구체 타입(AppDbContext)은 Infrastructure에 위치하여
            // Application 레이어에서 직접 참조 불가. 런타임 DI를 통해 등록된 AppDbContext를 반환
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();

            // ── 1단계: AuditLog 삭제 ──────────────────────────────────────
            await DeleteAuditLogsAsync(db, opts, ct);

            // ── 2단계: RateLimitLog 삭제 ──────────────────────────────────
            await DeleteRateLimitLogsAsync(db, opts, ct);

            // ── 3단계: BanLog ActorIp 익명화 ──────────────────────────────
            await AnonymizeBanLogActorIpAsync(db, opts, ct);

            // ── 4단계: IapPurchase ClientIp 익명화 ────────────────────────
            await AnonymizeIapPurchaseClientIpAsync(db, opts, ct);

            var elapsed = DateTime.UtcNow - totalStart;
            _logger.LogInformation(
                "PiiRetentionCleanup 완료 — 총 소요 시간: {Elapsed:0.0}초",
                elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            // 예외가 서비스 루프를 중단시키지 않도록 로그만 기록
            _logger.LogError(ex, "PiiRetentionCleanup 실행 중 예외 발생 — 다음 주기에 재시도");
        }
    }

    // AuditLog 보관 기간 초과 행 hard delete (청크 while 루프)
    // ExecuteDelete는 단일 SQL DELETE — 자동 트랜잭션 적용
    private async Task DeleteAuditLogsAsync(DbContext db, PiiRetentionOptions opts, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-opts.AuditLogDays);
        var stepStart = DateTime.UtcNow;
        var totalDeleted = 0;

        while (true)
        {
            // 청크 단위 삭제 — 한 번에 BatchSize 행까지만 삭제하여 DB 락 최소화
            var deleted = await db.Set<AuditLog>()
                .Where(l => l.CreatedAt < cutoff)
                .Take(opts.BatchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;

            // 삭제된 행이 BatchSize 미만이면 더 이상 대상 없음 → 종료
            if (deleted < opts.BatchSize) break;
        }

        var elapsed = DateTime.UtcNow - stepStart;
        _logger.LogInformation(
            "AuditLog 정리 완료 — 삭제: {Count}행, 소요: {Elapsed:0}ms, 기준일: {Cutoff:yyyy-MM-dd}",
            totalDeleted, elapsed.TotalMilliseconds, cutoff);
    }

    // RateLimitLog 보관 기간 초과 행 hard delete (청크 while 루프)
    private async Task DeleteRateLimitLogsAsync(DbContext db, PiiRetentionOptions opts, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-opts.RateLimitLogDays);
        var stepStart = DateTime.UtcNow;
        var totalDeleted = 0;

        while (true)
        {
            // 청크 단위 삭제 — OccurredAt 기준
            var deleted = await db.Set<RateLimitLog>()
                .Where(l => l.OccurredAt < cutoff)
                .Take(opts.BatchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deleted;

            if (deleted < opts.BatchSize) break;
        }

        var elapsed = DateTime.UtcNow - stepStart;
        _logger.LogInformation(
            "RateLimitLog 정리 완료 — 삭제: {Count}행, 소요: {Elapsed:0}ms, 기준일: {Cutoff:yyyy-MM-dd}",
            totalDeleted, elapsed.TotalMilliseconds, cutoff);
    }

    // BanLog ActorIp NULL 익명화 — 행은 영구 보존, IP만 NULL 처리
    // ExecuteUpdate는 단일 SQL UPDATE — 자동 트랜잭션 적용
    private async Task AnonymizeBanLogActorIpAsync(DbContext db, PiiRetentionOptions opts, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-opts.BanLogActorIpDays);
        var stepStart = DateTime.UtcNow;
        var totalUpdated = 0;

        while (true)
        {
            // 청크 단위 익명화 — ActorIp가 아직 NULL이 아닌 행만 대상
            var updated = await db.Set<BanLog>()
                .Where(b => b.CreatedAt < cutoff && b.ActorIp != null)
                .Take(opts.BatchSize)
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(b => b.ActorIp, (string?)null),
                    ct);

            totalUpdated += updated;

            if (updated < opts.BatchSize) break;
        }

        var elapsed = DateTime.UtcNow - stepStart;
        _logger.LogInformation(
            "BanLog ActorIp 익명화 완료 — 처리: {Count}행, 소요: {Elapsed:0}ms, 기준일: {Cutoff:yyyy-MM-dd}",
            totalUpdated, elapsed.TotalMilliseconds, cutoff);
    }

    // IapPurchase ClientIp NULL 익명화 — 상태 종결(Granted/Refunded/Failed) + 보관 기간 초과 조건
    // 본문(구매 이력)은 전자상거래법 §6에 따라 5년 보존, ClientIp만 익명화
    private async Task AnonymizeIapPurchaseClientIpAsync(DbContext db, PiiRetentionOptions opts, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-opts.IapPurchaseClientIpDays);
        var stepStart = DateTime.UtcNow;
        var totalUpdated = 0;

        // 상태 종결 조건 — 처리 완료된 구매만 익명화 (Pending/Verified는 아직 처리 중)
        var terminalStatuses = new[]
        {
            IapPurchaseStatus.Granted,
            IapPurchaseStatus.Refunded,
            IapPurchaseStatus.Failed
        };

        while (true)
        {
            // UpdatedAt 기준: 마지막 상태 변경 후 기간 초과 + ClientIp가 아직 NULL이 아닌 행
            var updated = await db.Set<IapPurchase>()
                .Where(p => p.ClientIp != null
                    && terminalStatuses.Contains(p.Status)
                    && p.UpdatedAt < cutoff)
                .Take(opts.BatchSize)
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(p => p.ClientIp, (string?)null),
                    ct);

            totalUpdated += updated;

            if (updated < opts.BatchSize) break;
        }

        var elapsed = DateTime.UtcNow - stepStart;
        _logger.LogInformation(
            "IapPurchase ClientIp 익명화 완료 — 처리: {Count}행, 소요: {Elapsed:0}ms, 기준일: {Cutoff:yyyy-MM-dd}",
            totalUpdated, elapsed.TotalMilliseconds, cutoff);
    }
}
