namespace Framework.Application.Features.Shop;

// 인게임 상점 구매 서비스 인터페이스 (플레이어 전용)
public interface IShopPurchaseService
{
    // 활성 상품 목록 조회 — 클라이언트 상점 화면 표시용
    Task<List<ShopProductDto>> GetActiveProductsAsync();

    // 상품 구매 처리 — 재화 차감 + 보상 지급 단일 트랜잭션
    // playerId: 구매 주체 플레이어 ID
    // productId: 구매할 ShopProduct ID
    // clientRequestId: 클라이언트 생성 UUID — 중복 처리 방지(멱등성) 보장
    Task<ShopPurchaseResult> BuyAsync(int playerId, int productId, string clientRequestId);
}
