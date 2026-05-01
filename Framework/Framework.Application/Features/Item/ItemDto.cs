using Framework.Domain.Enums;

namespace Framework.Application.Features.Item;

// 아이템 조회 응답 DTO
// 통화 아이템 여부는 ItemType == ItemType.Currency 로 판별
public record ItemDto(int Id, string Name, ItemType ItemType, string Description, AuditLevel AuditLevel, int AnomalyThreshold);

// 아이템 생성 요청 DTO
public record CreateItemDto(string Name, ItemType ItemType, string Description, AuditLevel AuditLevel, int AnomalyThreshold);

// 아이템 수정 요청 DTO
public record UpdateItemDto(string Name, ItemType ItemType, string Description, AuditLevel AuditLevel, int AnomalyThreshold);

// 플레이어 보유 아이템 DTO — 통화 아이템(ItemType.Currency) 포함 인벤토리 전체 목록
public record PlayerItemDto(int ItemId, string ItemName, ItemType ItemType, int Quantity);
