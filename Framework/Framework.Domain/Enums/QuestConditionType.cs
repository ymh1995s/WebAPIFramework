namespace Framework.Domain.Enums;

// 퀘스트 달성 조건 타입 — DB 저장값이므로 기존 값 변경 금지
public enum QuestConditionType : short
{
    // 스테이지 클리어 횟수 (재클리어 포함)
    StageCleared = 1,

    // 아이템 사용 횟수/수량
    ItemUsed = 2,

    // 상점 구매 횟수/수량
    ShopPurchased = 3,

    // 로그인 횟수
    Login = 4,
}
