namespace Framework.Application.Features.AdminPlayer;

// 플레이어 하드삭제 결과 열거형
// 컨트롤러에서 HTTP 상태 코드로 매핑하여 클라이언트에 반환
public enum HardDeleteResult
{
    // 삭제 성공
    Success,

    // 대상 플레이어를 찾을 수 없음 (404)
    NotFound,

    // 소프트 딜리트(탈퇴 처리)되지 않은 계정 — 하드삭제 불가 (409)
    NotWithdrawn
}
