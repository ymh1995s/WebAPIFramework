using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Framework.Application.Services;

// 일일 로그인 보상 서비스 구현체
public class DailyLoginService : IDailyLoginService
{
    private readonly IDailyLoginLogRepository _loginLogRepository;
    private readonly IDailyRewardConfigRepository _rewardConfigRepository;
    private readonly IMailService _mailService;
    private readonly IPlayerRecordRepository _playerRepository;
    private readonly ISystemConfigService _systemConfigService;

    public DailyLoginService(
        IDailyLoginLogRepository loginLogRepository,
        IDailyRewardConfigRepository rewardConfigRepository,
        IMailService mailService,
        IPlayerRecordRepository playerRepository,
        ISystemConfigService systemConfigService)
    {
        _loginLogRepository = loginLogRepository;
        _rewardConfigRepository = rewardConfigRepository;
        _mailService = mailService;
        _playerRepository = playerRepository;
        _systemConfigService = systemConfigService;
    }

    // 클라이언트 로그인 시 - 오늘 보상 미수령 시 우편 발송
    // [순서] 로그를 먼저 저장하여 (PlayerId, LoginDate) 유니크 인덱스로 동시 요청 중복 지급을 차단한 뒤 메일 발송
    public async Task<bool> ProcessLoginRewardAsync(int playerId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _loginLogRepository.ExistsAsync(playerId, today)) return false;

        var reward = await _rewardConfigRepository.GetDefaultAsync();
        if (reward is null) return false;

        // 로그를 먼저 기록 — 동시 요청이 들어와도 유니크 인덱스로 한 쪽만 성공
        try
        {
            await _loginLogRepository.AddAsync(new DailyLoginLog { PlayerId = playerId, LoginDate = today });
            await _loginLogRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 같은 날 중복 기록 시도 (동시 요청) — 이미 다른 쪽에서 처리 중이거나 완료됨
            return false;
        }

        // 로그 확정 후 메일 발송
        await _mailService.SendAsync(new SendMailDto(playerId, "일일 로그인 보상", "오늘의 로그인 보상입니다.", reward.ItemId, reward.ItemCount));
        return true;
    }

    // 스케줄러 - 자동 발송 활성화 시 전체 플레이어에게 일괄 발송
    public async Task ProcessDailyRewardForAllAsync()
    {
        var enabled = await _systemConfigService.GetDailyRewardEnabledAsync();
        if (!enabled) return;

        var reward = await _rewardConfigRepository.GetDefaultAsync();
        if (reward is null) return;

        await _mailService.BulkSendAsync(new BulkSendMailDto("일일 로그인 보상", "오늘의 로그인 보상입니다.", reward.ItemId, reward.ItemCount));
    }
}
