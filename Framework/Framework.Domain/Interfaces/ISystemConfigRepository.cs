using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 시스템 설정 저장소 인터페이스
public interface ISystemConfigRepository
{
    // Key에 해당하는 설정값 조회, 없으면 null 반환
    Task<string?> GetValueAsync(string key);
    // Key에 값 저장 (없으면 INSERT, 있으면 UPDATE)
    Task SetValueAsync(string key, string value);
    // 변경사항 저장
    Task SaveChangesAsync();
}
