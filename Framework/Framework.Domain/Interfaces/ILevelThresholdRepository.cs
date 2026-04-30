using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 레벨 임계값 저장소 인터페이스 — 조회 및 일괄 교체 연산 정의
public interface ILevelThresholdRepository
{
    // 모든 레벨 임계값을 레벨 오름차순으로 조회
    Task<List<LevelThreshold>> GetAllOrderedAsync();

    // 기존 데이터 전체를 삭제하고 새 목록으로 교체 (트랜잭션 보장)
    Task ReplaceAllAsync(List<LevelThreshold> items);
}
