namespace Framework.Application.Interfaces;

// 시스템 설정 서비스 인터페이스
public interface ISystemConfigService
{
    // 일일 보상 자동 발송 활성화 여부 조회
    Task<bool> GetDailyRewardEnabledAsync();
    // 일일 보상 자동 발송 활성화 여부 변경
    Task SetDailyRewardEnabledAsync(bool enabled);
}
