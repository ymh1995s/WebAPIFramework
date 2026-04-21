using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 일일 로그인 기록 저장소 인터페이스
public interface IDailyLoginLogRepository
{
    // 특정 플레이어가 해당 날짜에 이미 보상을 받았는지 확인
    Task<bool> ExistsAsync(int playerId, DateOnly date);
    // 로그인 기록 추가
    Task AddAsync(DailyLoginLog log);
    // 변경사항 저장
    Task SaveChangesAsync();
}
