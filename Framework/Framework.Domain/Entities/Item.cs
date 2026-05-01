using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 아이템 마스터 데이터
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ItemType ItemType { get; set; }
    public string Description { get; set; } = "";
    // 소프트 삭제 플래그 (true면 비활성화)
    public bool IsDeleted { get; set; } = false;

    // 감사 로그 기록 수준 — 기본은 AnomalyOnly (일반 재화)
    public AuditLevel AuditLevel { get; set; } = AuditLevel.AnomalyOnly;

    // AnomalyOnly일 때만 사용 — 1회 변동이 이 값을 넘으면 이상치로 기록
    // 0이면 이상치 검사를 하지 않음 (AnomalyOnly일 때 사실상 로그 비기록)
    public int AnomalyThreshold { get; set; } = 0;

}
