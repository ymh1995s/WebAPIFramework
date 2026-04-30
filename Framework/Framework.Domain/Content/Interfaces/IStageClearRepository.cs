// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Domain.Content.Entities;

namespace Framework.Domain.Content.Interfaces;

// 플레이어 스테이지 클리어 기록 저장소 인터페이스
public interface IStageClearRepository
{
    // PlayerId + StageId로 클리어 기록 조회 (없으면 null)
    Task<StageClear?> FindAsync(int playerId, int stageId);

    // 특정 플레이어의 전체 클리어 기록 조회
    Task<List<StageClear>> GetByPlayerIdAsync(int playerId);

    // 클리어 기록 추가
    Task AddAsync(StageClear stageClear);

    // 변경사항 저장
    Task SaveChangesAsync();
}
