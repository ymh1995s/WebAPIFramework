namespace Framework.Application.Features.Notice;

// 클라이언트용 — 최신 공지 응답 (Id는 클라이언트가 PlayerPrefs에 저장해 중복 표시 방지)
public record NoticeDto(int Id, string Content);

// Admin용 — 전체 정보 응답
public record NoticeAdminDto(int Id, string Content, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);

// Admin 공지 생성 요청
public record CreateNoticeDto(string Content);

// Admin 공지 수정 요청
public record UpdateNoticeDto(string Content, bool IsActive);
