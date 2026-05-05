using Framework.Application.Common;
using Framework.Application.Features.AdminNotification;
using Framework.Application.Features.DailyReward;
using Framework.Application.Features.Reward;
using Framework.Application.Features.SystemConfig;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Framework.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.DailyLogin;

// 일일 로그인 보상 서비스 구현체 — Current/Next 2슬롯 방식
// [트랜잭션 전략] 1차 트랜잭션: DailyLoginLog INSERT + AttendanceCount 증가
//                2차 호출: IRewardDispatcher.GrantAsync (자체 트랜잭션 + retry 내장)
// [실패 처리] 2차(보상 지급) 실패 시 LogError + AdminNotification 등록 — DailyLoginLog UNIQUE로 재시도 차단
public class DailyLoginService : IDailyLoginService
{
    private readonly IDailyLoginLogRepository _loginLogRepository;
    private readonly IDailyRewardSlotRepository _slotRepository;
    private readonly IDailyRewardSlotService _slotService;
    private readonly IRewardDispatcher _dispatcher;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISystemConfigService _systemConfigService;
    private readonly IAdminNotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyLoginService> _logger;

    // KST 오프셋 상수 (UTC+9)
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

    public DailyLoginService(
        IDailyLoginLogRepository loginLogRepository,
        IDailyRewardSlotRepository slotRepository,
        IDailyRewardSlotService slotService,
        IRewardDispatcher dispatcher,
        IPlayerRepository playerRepository,
        ISystemConfigService systemConfigService,
        IAdminNotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<DailyLoginService> logger)
    {
        _loginLogRepository = loginLogRepository;
        _slotRepository = slotRepository;
        _slotService = slotService;
        _dispatcher = dispatcher;
        _playerRepository = playerRepository;
        _systemConfigService = systemConfigService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // 클라이언트 로그인 시 — 오늘 보상 미수령 시 우편 발송
    // [1차 트랜잭션] DailyLoginLog INSERT + AttendanceCount 증가 — 커밋
    // [2차 호출] IRewardDispatcher.GrantAsync — 자체 트랜잭션 + retry 내장
    // [실패 분리] 2차 실패 시 운영자 알림 등록 후 false 반환 (1차 커밋은 유지)
    public async Task<bool> ProcessLoginRewardAsync(int playerId)
    {
        // 관리자 설정 기준 시각(KST)으로 게임 날짜 계산
        var boundaryHour = await _systemConfigService.GetDailyRewardDayBoundaryHourKstAsync();
        var boundaryMinute = await _systemConfigService.GetDailyRewardDayBoundaryMinuteKstAsync();
        var today = GetGameDay(DateTime.UtcNow, boundaryHour, boundaryMinute);

        // 오늘 이미 수령했으면 중단
        if (await _loginLogRepository.ExistsAsync(playerId, today)) return false;

        // 플레이어 조회
        var player = await _playerRepository.GetByIdAsync(playerId);
        if (player is null) return false;

        // 월 전환 체크 — 필요하면 Next → Current 복사 및 활성 연월 갱신
        await _slotService.EnsureMonthTransitionAsync();

        // 이번 달 로그인 횟수로 cycleDay 결정 (로그 기록 전이므로 현재까지의 누적 횟수)
        var monthlyCount = await _loginLogRepository.CountByPlayerAndMonthAsync(playerId, today.Year, today.Month);

        // 보상 번들 구성
        int? rewardItemId;
        int rewardItemCount;
        int rewardDayForLog;
        string mailBody;

        if (monthlyCount >= 28)
        {
            // 이번 달 28회 초과 — Admin 설정 기본 보상 지급
            rewardItemId = await _systemConfigService.GetDailyRewardDefaultItemIdAsync();
            rewardItemCount = await _systemConfigService.GetDailyRewardDefaultItemCountAsync();
            rewardDayForLog = 0;
            mailBody = "기본 보상입니다.";
        }
        else
        {
            // 슬롯 보상 — 이번 달 N번째 로그인 = Day N 보상 (monthlyCount + 1)
            var cycleDay = monthlyCount + 1;
            var slot = await _slotRepository.GetSlotDayAsync(RewardSlotKind.Current, cycleDay);
            rewardItemId = slot?.ItemId;
            rewardItemCount = slot?.ItemCount ?? 0;
            rewardDayForLog = cycleDay;
            mailBody = $"출석 {cycleDay}일차 보상입니다.";
        }

        // ── 1차 트랜잭션: 로그 INSERT + 출석 카운터 증가 ───────────────
        // 로그 먼저 기록 — (PlayerId, LoginDate) UNIQUE 제약으로 동시 요청 중복 차단
        // 이 트랜잭션을 먼저 커밋해야 2차 보상 지급 실패 시에도 중복 지급 방지 가능
        var logCommitted = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var loginLog = new DailyLoginLog
            {
                PlayerId = playerId,
                LoginDate = today,
                RewardDay = rewardDayForLog
            };

            try
            {
                await _loginLogRepository.AddAsync(loginLog);
                await _loginLogRepository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 같은 날 중복 기록 시도 (동시 요청) — DetachEntry 후 중단
                _unitOfWork.DetachEntry(loginLog);
                return false;
            }

            // 누적 출석 카운터 증가 (통계용)
            await _playerRepository.IncrementAttendanceCountAsync(new[] { playerId });

            return true;
        });

        // 1차 트랜잭션 실패(중복) 시 종료
        if (!logCommitted) return false;

        // ── 2차 호출: 보상 지급 (트랜잭션 외부) ──────────────────────────
        // 보상 아이템이 설정되어 있을 때만 지급
        if (!rewardItemId.HasValue || rewardItemCount <= 0)
            return true;

        // SourceKey: SourceKeys.DailyLogin — 게임 날짜 기반 멱등성 키
        var sourceKey = SourceKeys.DailyLogin(today);
        var bundle = new RewardBundle(Items: new[] { new RewardItem(rewardItemId.Value, rewardItemCount) });

        // [중요] DailyLogin은 Obsolete 처리된 enum 값이나, 일일 로그인 경로 식별을 위해 유지
        // RewardTables 생성 시에는 사용 금지 — 여기서는 SourceType 식별 목적으로만 사용
#pragma warning disable CS0618 // DailyLogin enum은 Obsolete이나 일일 로그인 SourceType 식별 목적으로만 사용
        var request = new GrantRewardRequest(
            PlayerId: playerId,
            SourceType: RewardSourceType.DailyLogin,
            SourceKey: sourceKey,
            Bundle: bundle,
            MailTitle: "일일 로그인 보상",
            MailBody: mailBody,
            Mode: DispatchMode.Mail
        );
#pragma warning restore CS0618

        try
        {
            var result = await _dispatcher.GrantAsync(request);

            if (!result.Success && !result.AlreadyGranted)
            {
                // 보상 지급 실패 — 운영자 알림 등록 (수동 우편 발송 필요)
                _logger.LogError(
                    "DailyLogin 보상 지급 실패 — PlayerId: {PlayerId}, Date: {Date}, Message: {Message}",
                    playerId, today, result.Message);

                await _notificationService.CreateAsync(
                    category: AdminNotificationCategory.RewardDispatchFailure,
                    severity: AdminNotificationSeverity.Critical,
                    title: "DailyLogin 보상 누락",
                    message: $"DailyLogin 보상 누락 — PlayerId={playerId}, Date={today:yyyy-MM-dd}",
                    relatedEntityType: "DailyLoginLog",
                    relatedEntityId: playerId,
                    dedupKey: AdminNotificationDedupKeys.DailyLoginFail(playerId, today));

                return false;
            }
        }
        catch (Exception ex)
        {
            // 예외 발생 시에도 운영자 알림 등록 — 로그와 함께 기록
            _logger.LogError(
                ex,
                "DailyLogin 보상 지급 중 예외 발생 — PlayerId: {PlayerId}, Date: {Date}",
                playerId, today);

            await _notificationService.CreateAsync(
                category: AdminNotificationCategory.RewardDispatchFailure,
                severity: AdminNotificationSeverity.Critical,
                title: "DailyLogin 보상 누락",
                message: $"DailyLogin 보상 누락 — PlayerId={playerId}, Date={today:yyyy-MM-dd}",
                relatedEntityType: "DailyLoginLog",
                relatedEntityId: playerId,
                dedupKey: AdminNotificationDedupKeys.DailyLoginFail(playerId, today));

            return false;
        }

        return true;
    }

    // KST 기준 시각을 적용하여 게임 날짜를 계산한다.
    // 기준 시각 미만이면 아직 전날로 간주 — 예) 기준 06:00, 현재 KST 05:30 → 어제 날짜 반환
    private static DateOnly GetGameDay(DateTime utcNow, int boundaryHourKst, int boundaryMinuteKst)
    {
        // UTC → KST 변환 (UTC+9)
        var kstNow = utcNow.AddHours(9);
        var kstTime = TimeOnly.FromDateTime(kstNow);
        var kstDate = DateOnly.FromDateTime(kstNow);
        var boundary = new TimeOnly(boundaryHourKst, boundaryMinuteKst);
        // 기준 시각 미만이면 전날을 게임 날짜로 반환
        return kstTime < boundary ? kstDate.AddDays(-1) : kstDate;
    }
}
