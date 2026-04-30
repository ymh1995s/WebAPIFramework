namespace Framework.Application.Features.Shout;

// 1회 공지 서비스 인터페이스
public interface IShoutService
{
    // 클라이언트 접속 시 활성 1회 공지 조회
    Task<List<ShoutDto>> GetActiveForPlayerAsync(int playerId);

    // Admin 1회 공지 생성
    Task<ShoutAdminDto> CreateAsync(CreateShoutDto dto);

    // Admin 이력 조회
    Task<ShoutListResponse> GetAllAsync(int page, int pageSize, int? playerId, bool? activeOnly);

    // Admin 즉시 비활성화
    Task<bool> DeactivateAsync(int id);
}
