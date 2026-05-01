namespace Framework.Application.Features.AdminPlayer;

// 밴/밴해제 작업 결과 — 상태 전이 검증에 사용
public enum BanOperationResult
{
    Success        = 0, // 정상 처리
    PlayerNotFound = 1, // 대상 플레이어 없음
    AlreadyBanned  = 2, // 이미 밴 상태 — 중복 밴 불가
    NotBanned      = 3  // 밴 상태 아님 — 밴해제 불가
}
