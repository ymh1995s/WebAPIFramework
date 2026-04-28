using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 일일 로그인 기록 저장소 인터페이스
public interface IDailyLoginLogRepository
{
    // 특정 플레이어가 해당 날짜에 이미 보상을 받았는지 확인
    Task<bool> ExistsAsync(int playerId, DateOnly date);
    // 로그인 기록 추가
    Task AddAsync(DailyLoginLog log);
    // 다수 로그인 기록 일괄 추가 (배치 처리용)
    Task AddRangeAsync(IEnumerable<DailyLoginLog> logs);
    // 특정 플레이어의 지정 연/월 로그인 횟수 조회 (기본 보상 분기 판단용)
    Task<int> CountByPlayerAndMonthAsync(int playerId, int year, int month);
    // 변경사항 저장
    Task SaveChangesAsync();
}
