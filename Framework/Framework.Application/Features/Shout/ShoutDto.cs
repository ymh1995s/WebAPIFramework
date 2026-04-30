namespace Framework.Application.Features.Shout;

// 클라이언트 응답 DTO — HUD 표시에 필요한 최소 정보
public record ShoutDto(int Id, string Message, DateTime CreatedAt, DateTime ExpiresAt);

// Admin 응답 DTO — 관리에 필요한 전체 정보
public record ShoutAdminDto(int Id, int? PlayerId, string Message, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive);

// Admin 생성 요청 DTO — PlayerId null이면 전체 대상
public record CreateShoutDto(int? PlayerId, string Message, int ExpiresInMinutes = 60);

// Admin 목록 응답 DTO
public record ShoutListResponse(List<ShoutAdminDto> Items, int Total);
