namespace Framework.Domain.Constants;

// 통화 아이템 ID 상수 — Item 마스터 테이블의 고정 ItemId (ItemType.Currency 행)
// 새 통화 추가 시 이 파일에 상수와 Item 마스터 데이터만 추가하면 되고 코드 변경 불필요
public static class CurrencyIds
{
    // 소프트 재화 (골드) — Item 마스터 ID=1
    public const int Gold = 1;

    // 하드 재화 (젬) — Item 마스터 ID=2
    public const int Gems = 2;
}
