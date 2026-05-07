using System.ComponentModel.DataAnnotations;

namespace Framework.Application.Features.AdminRewardDispatch;

// 보상 지급 취소 요청 DTO
// Direct 지급(MailId=null)은 서비스 레이어에서 422로 거부됨
public record CancelRewardDto(
    // 취소 사유 — 필수 입력, Admin 감사 로그에 영구 기록됨
    [Required]
    [MaxLength(500)]
    string Reason,

    // 플레이어에게 취소 안내 우편 발송 여부 — 선택 사항
    bool SendMailNotification = false,

    // 안내 우편 제목 (SendMailNotification=true 시 사용) — 기본값은 서비스 레이어에서 설정
    [MaxLength(100)]
    string? NotificationMailTitle = null,

    // 안내 우편 본문 (SendMailNotification=true 시 사용)
    [MaxLength(2000)]
    string? NotificationMailBody = null
);

// 보상 지급 취소 결과 DTO
public record CancelRewardResult(
    // 취소 처리 성공 여부
    bool Success,

    // 처리 결과 메시지
    string Message,

    // 이미 취소된 상태인지 여부
    bool AlreadyCancelled = false,

    // 지급 이력을 찾지 못했는지 여부
    bool NotFound = false,

    // Direct 지급이라 취소 불가한 경우
    bool IsDirectGrant = false,

    // 우편이 이미 수령된 상태라 취소 불가한 경우
    bool AlreadyClaimed = false
);
