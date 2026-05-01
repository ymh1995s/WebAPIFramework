namespace Framework.Application.Features.Exp;

// 런타임 레벨 계산 진입점 — 캐시 기반 조회로 매 요청마다 DB 호출 없이 레벨 계산 가능
// Singleton으로 등록하여 캐시를 프로세스 전역에서 공유
public interface ILevelTableProvider
{
    // 현재 테이블에 정의된 최대 레벨 번호
    int MaxLevel { get; }

    // 누적 경험치로 현재 레벨 계산
    int CalcLevel(int totalExp);

    // 특정 레벨에 도달하기 위한 누적 경험치 임계값 반환
    // 테이블에 없는 레벨이면 int.MaxValue 반환
    int GetThreshold(int level);

    // 캐시를 무효화하여 다음 조회 시 DB에서 최신 데이터를 다시 로드하도록 강제
    void Invalidate();
}
