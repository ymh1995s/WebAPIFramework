// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

using Framework.Domain.Content.Entities;

namespace Framework.Domain.Content.Interfaces;

// 스테이지 마스터 저장소 인터페이스
public interface IStageRepository
{
    // ID로 스테이지 단건 조회 (비활성 포함)
    Task<Stage?> GetByIdAsync(int id);

    // Code로 스테이지 단건 조회
    Task<Stage?> GetByCodeAsync(string code);

    // 활성 스테이지 전체 목록 조회 (SortOrder 오름차순)
    Task<List<Stage>> GetAllActiveAsync();

    // 전체 스테이지 조회 — Admin 관리 목적 (비활성 포함, 페이지네이션)
    Task<(List<Stage> Items, int TotalCount)> SearchAsync(string? keyword, int page, int pageSize);

    // 스테이지 추가
    Task AddAsync(Stage stage);

    // 변경사항 저장
    Task SaveChangesAsync();
}
