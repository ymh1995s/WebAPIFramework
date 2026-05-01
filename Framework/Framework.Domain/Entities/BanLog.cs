using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 밴/밴해제 감사 이력 엔티티 — Admin 처리 이력 영구 보존
// PlayerId FK 미설정: 플레이어 하드 삭제 후에도 이력 보존 목적
public class BanLog
{
    // 기본 키 (BIGSERIAL — 대용량 이력 대비)
    public long Id { get; set; }

    // 처리 대상 플레이어 ID — 논리 참조 (FK 없음)
    public int PlayerId { get; set; }

    // 처리 액션 (Ban=1, Unban=2)
    public BanAction Action { get; set; }

    // Ban 액션 시 만료 시각 (UTC) — null이면 영구 밴. Unban일 때는 null
    public DateTime? BannedUntil { get; set; }

    // Admin이 입력한 처리 사유 — nullable, 입력 권장이나 강제 아님
    public string? Reason { get; set; }

    // 행위자 유형 — 현재는 Admin=1 고정. 향후 시스템 자동 밴(System=2) 확장 대비
    public AuditActorType ActorType { get; set; } = AuditActorType.Admin;

    // 행위자 ID — 향후 Admin 다계정 도입 시 채움. 현재는 null
    public int? ActorId { get; set; }

    // 처리 요청 IP — Admin 침투 사고 조사 대비 (IPv6 포함 최대 45자)
    public string? ActorIp { get; set; }

    // 기록 생성 시각 (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
