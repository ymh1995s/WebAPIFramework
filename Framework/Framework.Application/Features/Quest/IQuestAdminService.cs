using Framework.Application.Common;
using Framework.Domain.Enums;

namespace Framework.Application.Features.Quest;

// 퀘스트 Admin 관리 서비스 인터페이스
public interface IQuestAdminService
{
    // 퀘스트 정의 목록 조회 — Admin 검색 (키워드 + 주기 필터 + 페이지네이션)
    Task<PagedResultDto<QuestDefinitionDto>> SearchAsync(
        string? keyword, QuestPeriod? period, bool? isActive, int page, int pageSize);

    // 퀘스트 정의 단건 조회
    Task<QuestDefinitionDto?> GetByIdAsync(int id);

    // 퀘스트 정의 생성
    Task<QuestDefinitionDto> CreateAsync(CreateQuestDefinitionDto dto);

    // 퀘스트 정의 수정 (Period/Code는 변경 불가)
    Task<bool> UpdateAsync(int id, UpdateQuestDefinitionDto dto);

    // 퀘스트 정의 소프트 삭제
    Task<bool> DeleteAsync(int id);

    // 특정 플레이어의 퀘스트 진행 상태 조회 — Admin 플레이어 상세 화면용
    Task<List<QuestProgressDto>> GetPlayerProgressAsync(int playerId);
}
