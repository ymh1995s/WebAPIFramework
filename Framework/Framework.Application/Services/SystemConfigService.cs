using Framework.Application.Interfaces;
using Framework.Domain.Constants;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

// 시스템 설정 서비스 구현체
public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _repository;

    public SystemConfigService(ISystemConfigRepository repository)
    {
        _repository = repository;
    }

    // DB에서 일일 보상 활성화 여부 조회 ("true" 문자열과 비교)
    public async Task<bool> GetDailyRewardEnabledAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyLoginRewardEnabled);
        return value == "true";
    }

    // 일일 보상 활성화 여부를 DB에 저장
    public async Task SetDailyRewardEnabledAsync(bool enabled)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyLoginRewardEnabled, enabled ? "true" : "false");
        await _repository.SaveChangesAsync();
    }
}
