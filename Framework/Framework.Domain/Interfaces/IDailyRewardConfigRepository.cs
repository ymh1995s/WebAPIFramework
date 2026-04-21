using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 일일 보상 설정 저장소 인터페이스
public interface IDailyRewardConfigRepository
{
    // 연속 출석일수에 해당하는 보상 설정 조회
    Task<DailyRewardConfig?> GetByDayAsync(int day);
    // 기본 보상 조회 (Day 오름차순 첫 번째 - 현재는 단일 보상 구조)
    Task<DailyRewardConfig?> GetDefaultAsync();
    // 변경사항 저장
    Task SaveChangesAsync();
}
