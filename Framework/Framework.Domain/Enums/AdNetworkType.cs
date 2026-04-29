namespace Framework.Domain.Enums;

// 광고 네트워크 종류 — 새 네트워크 추가 시 enum 값만 확장
// 주의: DB에 정수값으로 저장되므로 기존 값은 절대 변경하지 말 것
public enum AdNetworkType
{
    // Unity Ads 광고 네트워크
    UnityAds = 1,

    // IronSource 광고 네트워크
    IronSource = 2,
}
