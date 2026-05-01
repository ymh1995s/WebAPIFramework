using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 레벨 임계값 저장소 인터페이스 — 조회 및 일괄 교체 연산 정의
public interface ILevelThresholdRepository
{
    // 모든 레벨 임계값을 레벨 오름차순으로 조회
    Task<List<LevelThreshold>> GetAllOrderedAsync();

    // 기존 데이터 전체를 삭제하고 새 목록으로 교체 (변경 추적만 — 트랜잭션/저장은 호출자가 처리)
    Task ReplaceAllAsync(List<LevelThreshold> items);

    // 변경 사항을 DB에 저장 — 호출자가 SaveChanges 시점을 명시적으로 제어
    Task SaveChangesAsync();
}
