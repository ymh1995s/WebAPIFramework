using Framework.Application.Features.DailyReward;
using Framework.Application.Features.Mail;
using Framework.Application.Features.SystemConfig;
using Framework.Domain.Entities;
using Framework.Domain.Enums;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Framework.Application.Features.DailyLogin;

// 일일 로그인 보상 서비스 구현체 — Current/Next 2슬롯 방식
public class DailyLoginService : IDailyLoginService
{
    private readonly IDailyLoginLogRepository _loginLogRepository;
    private readonly IDailyRewardSlotRepository _slotRepository;
    private readonly IDailyRewardSlotService _slotService;
    private readonly IMailService _mailService;
    private readonly IPlayerRepository _playerRepository;
    private readonly ISystemConfigService _systemConfigService;

    // KST 오프셋 상수 (UTC+9)
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

    public DailyLoginService(
        IDailyLoginLogRepository loginLogRepository,
        IDailyRewardSlotRepository slotRepository,
        IDailyRewardSlotService slotService,
        IMailService mailService,
        IPlayerRepository playerRepository,
        ISystemConfigService systemConfigService)
    {
        _loginLogRepository = loginLogRepository;
        _slotRepository = slotRepository;
        _slotService = slotService;
        _mailService = mailService;
        _playerRepository = playerRepository;
        _systemConfigService = systemConfigService;
    }

    // 클라이언트 로그인 시 — 오늘 보상 미수령 시 우편 발송
    // [순서] 로그를 먼저 저장하여 (PlayerId, LoginDate) 유니크 인덱스로 동시 요청 중복 지급 차단 후 메일 발송
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

        // 로그를 먼저 기록 — (PlayerId, LoginDate) 유니크 인덱스로 동시 요청 중복 차단
        try
        {
            await _loginLogRepository.AddAsync(new DailyLoginLog
            {
                PlayerId = playerId,
                LoginDate = today,
                RewardDay = rewardDayForLog
            });
            await _loginLogRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 같은 날 중복 기록 시도 (동시 요청) — 이미 다른 쪽에서 처리 중이거나 완료됨
            return false;
        }

        // 누적 출석 카운터 증가 (통계용)
        await _playerRepository.IncrementAttendanceCountAsync(new[] { playerId });

        // 보상 아이템이 설정되어 있을 때만 메일 발송
        if (rewardItemId.HasValue && rewardItemCount > 0)
        {
            await _mailService.SendAsync(new SendMailDto(
                playerId,
                "일일 로그인 보상",
                mailBody,
                rewardItemId.Value,
                rewardItemCount
            ));
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
