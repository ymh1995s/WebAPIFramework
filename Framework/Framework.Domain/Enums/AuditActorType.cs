namespace Framework.Domain.Enums;

// 감사 로그 행위자 유형 — 재화/아이템 변동을 일으킨 주체를 구분
public enum AuditActorType
{
    Player = 0, // 플레이어 본인 행위 (우편 수령 등)
    Admin  = 1, // 관리자 직접 조작 (지급, 회수 등)
    System = 2  // 시스템 자동 처리 (일일 보상, 스테이지 클리어 등)
}
