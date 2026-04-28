namespace Framework.Application.Common;

// 플레이어 기록 서비스 인터페이스
public interface IPlayerRecordService
{
    // 전체 목록 조회
    Task<List<PlayerRecordDto>> GetAllAsync();
    // 페이지 단위 조회
    Task<PagedResultDto<PlayerRecordDto>> GetPagedAsync(int page, int pageSize);
    // ID로 단건 조회
    Task<PlayerRecordDto?> GetByIdAsync(int id);
    // 새 기록 생성
    Task CreateAsync(CreatePlayerRecordDto dto);
}
