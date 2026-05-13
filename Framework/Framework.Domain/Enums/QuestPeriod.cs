namespace Framework.Domain.Enums;

// 퀘스트 주기 — DB 저장값이므로 기존 값 변경 금지
public enum QuestPeriod : short
{
    // 일일 퀘스트 — KST 기준 날짜 초기화
    Daily = 0,

    // 주간 퀘스트 — KST 기준 주 초기화 (월요일 시작)
    Weekly = 1,

    // 메인 퀘스트 (영구) — 완료 후 리셋 없음
    Permanent = 2,
}
