using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 시스템 설정 저장소 인터페이스
public interface ISystemConfigRepository
{
    // Key에 해당하는 설정값 조회, 없으면 null 반환
    Task<string?> GetValueAsync(string key);
    // Key에 값 저장 (없으면 INSERT, 있으면 UPDATE)
    Task SetValueAsync(string key, string value);
    // 특정 접두사로 시작하는 모든 설정 항목 목록 반환
    Task<IReadOnlyList<SystemConfig>> GetByPrefixAsync(string prefix);
    // 특정 Key의 설정 항목 삭제 (없으면 아무 작업도 하지 않음)
    Task DeleteAsync(string key);
    // 변경사항 저장
    Task SaveChangesAsync();
}
