using Framework.Domain.Enums;

namespace Framework.Domain.Entities;

// 아이템 마스터 데이터
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ItemType ItemType { get; set; }
    public string Description { get; set; } = "";
}
