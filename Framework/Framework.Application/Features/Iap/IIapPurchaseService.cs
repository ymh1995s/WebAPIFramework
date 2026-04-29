namespace Framework.Application.Features.Iap;

// 인앱결제 검증 및 보상 지급 메인 서비스 인터페이스
// 구매 토큰 검증 → 중복 처리 방지 → 보상 지급 파이프라인을 담당
public interface IIapPurchaseService
{
    // 구매 영수증 검증 후 보상 지급
    // playerId: JWT에서 추출한 현재 플레이어 ID
    // request: 클라이언트가 전달한 구매 정보 (ProductId, PurchaseToken 등)
    // clientIp: 어뷰징 탐지용 클라이언트 IP (선택값)
    Task<IapVerifyResult> VerifyAndGrantAsync(int playerId, IapVerifyRequest request, string? clientIp);
}
