namespace Framework.Domain.Enums;

// 인앱결제 상품 유형 — Consumable(소모성)과 NonConsumable(비소모성)만 지원, Subscription 제외
public enum IapProductType
{
    // 소모성 상품 — 구매 후 소모 가능 (재화 충전 등)
    Consumable = 1,

    // 비소모성 상품 — 한 번 구매 후 영구 보유 (광고 제거 등)
    NonConsumable = 2
}
