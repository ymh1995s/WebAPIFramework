namespace Framework.Application.Features.Iap;

// RTDN(Real-time Developer Notifications) 처리 서비스 인터페이스
// Google Pub/Sub으로 수신된 환불/구매 알림을 처리하는 진입점
public interface IIapRtdnService
{
    // RTDN 페이로드를 받아 알림 유형에 맞는 처리를 수행
    // payload: base64 디코딩 + JSON 역직렬화된 RTDN 데이터
    Task HandleAsync(RtdnPayload payload);
}
