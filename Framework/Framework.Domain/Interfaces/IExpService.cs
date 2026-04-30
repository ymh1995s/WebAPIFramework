namespace Framework.Domain.Interfaces;

// 경험치 처리 서비스 인터페이스 — 경험치 획득 및 레벨업 로직의 단일 진입점
public interface IExpService
{
    // 경험치 추가 — 레벨업 발생 시 보상도 함께 처리
    // sourceKey: 멱등성/추적용 식별자 (예: "stage:5", "match:guid")
    Task AddExpAsync(int playerId, int expAmount, string sourceKey);
}
