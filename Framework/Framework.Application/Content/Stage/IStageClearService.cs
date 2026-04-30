// ============================================================
// [컨텐츠 영역] 본 파일은 게임 컨텐츠 코드입니다.
// Framework 영역은 이 코드를 참조해서는 안 됩니다.
// 의존 방향: Content → Framework (역방향 금지)
// ============================================================

namespace Framework.Application.Content.Stage;

// 스테이지 클리어 서비스 인터페이스
public interface IStageClearService
{
    // 스테이지 클리어 완료 처리 — 보상 지급 및 기록 upsert
    // 반환: 처리 결과 DTO
    // 예외: KeyNotFoundException (스테이지 없음/비활성), InvalidOperationException (선행 스테이지 미클리어)
    Task<StageClearResponseDto> CompleteAsync(int playerId, int stageId, StageClearRequestDto request);

    // 플레이어의 스테이지 진행 현황 목록 조회 (활성 스테이지 전체)
    Task<List<StageProgressDto>> GetProgressAsync(int playerId);

    // 활성 스테이지 목록 조회 (마스터 데이터)
    Task<List<StageDto>> GetActiveStagesAsync();

    // Admin — 스테이지 생성
    Task<StageDto> CreateStageAsync(CreateStageDto dto);

    // Admin — 스테이지 수정
    Task<bool> UpdateStageAsync(int id, UpdateStageDto dto);

    // Admin — 스테이지 전체 목록 조회 (페이지네이션)
    Task<(List<StageDto> Items, int TotalCount)> SearchAsync(string? keyword, int page, int pageSize);

    // Admin — 스테이지 단건 조회
    Task<StageDto?> GetByIdAsync(int id);
}
