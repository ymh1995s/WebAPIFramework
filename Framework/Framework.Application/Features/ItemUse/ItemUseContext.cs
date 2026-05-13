namespace Framework.Application.Features.ItemUse;

// 아이템 사용 컨텍스트 — IItemUseEffectExtension에 전달되는 실행 정보
// 게임 특화 확장 구현체가 필요한 모든 실행 정보를 담음
public record ItemUseContext(
    // 아이템을 사용한 플레이어 ID
    int PlayerId,

    // 사용한 아이템 마스터 ID
    int ItemId,

    // 사용한 아이템의 타입 (Domain.Enums.ItemType 정수값)
    int ItemType,

    // 사용한 수량 — N개 사용 시 확장 효과에서 수량 기반 로직 분기 가능
    int Quantity = 1
);
