namespace Framework.Application.Common;

// 단순 메시지 응답 — Admin 컨트롤러 전반 공용 (성공/실패 안내 메시지)
public record MessageResponse(string Message);

// 단순 카운트 응답 — 보유 유저 수, 미읽음 알림 수 등 공용
public record CountResponse(int Count);

// Admin 보상 지급 결과 응답 — 신규 지급/이미 지급 두 케이스를 단일 record로 표현
public record AdminGrantResponse(
    // 결과 메시지
    string Message,
    // 실제 사용된 지급 방식 (Direct/Mail, 이미 지급 시 null)
    string? UsedMode,
    // 우편 지급 시 생성된 MailId (Direct 지급 또는 이미 지급 시 null)
    int? MailId,
    // 이미 지급된 보상인지 여부
    bool AlreadyGranted = false
);
