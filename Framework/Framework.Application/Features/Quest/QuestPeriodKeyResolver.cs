using Framework.Domain.Enums;
using System.Globalization;

namespace Framework.Application.Features.Quest;

// 퀘스트 주기 키 계산 구현체 — KST(UTC+9) 기준
// Daily:     "2026-01-13"  → KST 날짜
// Weekly:    "2026-W03"    → ISO 8601 주차 (KST 기준, 월요일 주 시작)
// Permanent: "permanent"  → 고정 문자열
public class QuestPeriodKeyResolver : IQuestPeriodKeyResolver
{
    // 현재 UTC 시각을 KST로 변환하여 주기에 맞는 PeriodKey를 반환한다.
    public string Resolve(QuestPeriod period)
    {
        // UTC → KST 변환 (UTC+9)
        var kstNow = DateTime.UtcNow.AddHours(9);

        return period switch
        {
            // 일일: KST 날짜 문자열
            QuestPeriod.Daily => kstNow.ToString("yyyy-MM-dd"),

            // 주간: ISO 8601 주차 (CalendarWeekRule.FirstFourDayWeek, 월요일 시작)
            // 예) 2026년 3주차 → "2026-W03"
            QuestPeriod.Weekly => BuildWeeklyKey(kstNow),

            // 영구: 고정 문자열
            QuestPeriod.Permanent => "permanent",

            // 알 수 없는 주기 — 영구로 폴백 (방어 코드)
            _ => "permanent",
        };
    }

    // ISO 8601 주차 키 생성 — "yyyy-Www" 형식
    // ISOWeek 클래스(.NET 5+)를 사용하여 연도 경계(12월 말/1월 초)에서도 정확한 주차 반환
    private static string BuildWeeklyKey(DateTime kstDate)
    {
        // ISO 8601 주차 계산 — 연도 경계 처리 포함 (ISOWeek.GetYear, ISOWeek.GetWeekOfYear)
        var isoYear = ISOWeek.GetYear(DateOnly.FromDateTime(kstDate));
        var isoWeek = ISOWeek.GetWeekOfYear(DateOnly.FromDateTime(kstDate));
        return $"{isoYear}-W{isoWeek:D2}";
    }
}
