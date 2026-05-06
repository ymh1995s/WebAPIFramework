namespace Framework.Application.Features.Exp;

// 경험치 처리 서비스 인터페이스 — 경험치 획득 및 레벨업 로직의 단일 진입점
public interface IExpService
{
    // 경험치 추가 — 레벨업 발생 시 오른 레벨 번호 목록을 오름차순으로 반환
    // sourceKey: 멱등성/추적용 식별자 (예: "stage:5", "match:guid")
    // 반환값: 이번 호출로 도달한 레벨 번호 오름차순 리스트 (빈 리스트 = 레벨업 없음, 정상 시그널)
    Task<IReadOnlyList<int>> AddExpAsync(int playerId, int expAmount, string sourceKey);
}
