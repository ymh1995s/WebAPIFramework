namespace Framework.Application.Features.ItemUse;

// 아이템 사용(소모) 서비스 인터페이스
// 수량 차감 → 보상 지급(옵션) → 확장 효과(옵션) 흐름을 단일 진입점으로 관리
public interface IItemUseService
{
    // 아이템 사용 처리
    // playerId: 사용 주체 플레이어 ID
    // itemId: 사용할 아이템 마스터 ID
    // clientRequestId: 클라이언트 생성 UUID — 중복 처리 방지(멱등성) 보장
    // quantity: 사용 수량 (기본값 1)
    Task<ItemUseResult> UseItemAsync(int playerId, int itemId, string clientRequestId, int quantity = 1, CancellationToken ct = default);
}
