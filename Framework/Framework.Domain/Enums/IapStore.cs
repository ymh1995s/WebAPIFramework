namespace Framework.Domain.Enums;

// 인앱결제 스토어 종류 — 현재 Google Play만 운영, Apple은 예약 enum
public enum IapStore
{
    // Google Play 스토어
    Google = 1,

    // Apple App Store (향후 지원 예정)
    Apple = 2
}
