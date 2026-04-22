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
}
