namespace Framework.Application.Interfaces;

// 일일 로그인 보상 서비스 인터페이스
public interface IDailyLoginService
{
    // 클라이언트 로그인 시 호출 - 오늘 보상 미수령 시 우편 발송
    Task<bool> ProcessLoginRewardAsync(int playerId);
    // 스케줄러 호출 - 전체 플레이어에게 일괄 발송
    Task ProcessDailyRewardForAllAsync();
}
