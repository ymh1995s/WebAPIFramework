using Framework.Application.Common;

namespace Framework.Application.Features.RewardTable;

// 보상 테이블 Admin 관리 서비스 인터페이스
public interface IRewardTableService
{
    // 보상 테이블 목록 조회 (필터 + 페이지네이션)
    Task<PagedResultDto<RewardTableDto>> SearchAsync(RewardTableFilterDto filter);

    // ID로 단건 조회 (Entries 포함)
    Task<RewardTableDetailDto?> GetByIdAsync(int id);

    // 보상 테이블 생성 — 중복(UNIQUE 위반) 시 null 반환
    Task<RewardTableDetailDto?> CreateAsync(CreateRewardTableDto dto);

    // 보상 테이블 설명 수정
    Task<bool> UpdateAsync(int id, UpdateRewardTableDto dto);

    // 보상 테이블 소프트 삭제
    Task<bool> SoftDeleteAsync(int id);

    // Entries 일괄 교체 — 기존 항목 전체 삭제 후 신규 항목 삽입
    Task<bool> ReplaceEntriesAsync(int id, List<EntryUpsertDto> entries);
}
