using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

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
    public async Task<bool> ProcessLoginRewardAsync(int playerId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _loginLogRepository.ExistsAsync(playerId, today)) return false;

        var reward = await _rewardConfigRepository.GetDefaultAsync();
        if (reward is null) return false;

        await _mailService.SendAsync(new SendMailDto(playerId, "일일 로그인 보상", "오늘의 로그인 보상입니다.", reward.ItemId, reward.ItemCount));
        await _loginLogRepository.AddAsync(new DailyLoginLog { PlayerId = playerId, LoginDate = today });
        await _loginLogRepository.SaveChangesAsync();
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
