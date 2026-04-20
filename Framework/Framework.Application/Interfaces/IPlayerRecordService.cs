using Framework.Application.DTOs;

namespace Framework.Application.Interfaces;

// 플레이어 기록 서비스 인터페이스
public interface IPlayerRecordService
{
    // 전체 목록 조회
    Task<List<PlayerRecordDto>> GetAllAsync();
    // ID로 단건 조회
    Task<PlayerRecordDto?> GetByIdAsync(int id);
    // 새 기록 생성
    Task CreateAsync(CreatePlayerRecordDto dto);
}
