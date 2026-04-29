using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 보상 테이블 저장소 인터페이스
public interface IRewardTableRepository
{
    // SourceType + Code 조합으로 보상 테이블 조회 (항목 포함)
    Task<RewardTable?> FindAsync(RewardSourceType sourceType, string code);

    // 전체 보상 테이블 조회 (삭제된 항목 제외)
    Task<List<RewardTable>> GetAllAsync();

    // Admin 필터 검색 — sourceType, code 필터 + 페이지네이션 + 소프트 딜리트 제외
    Task<(List<RewardTable> Items, int TotalCount)> SearchAsync(
        RewardSourceType? sourceType, string? code, int page, int pageSize);

    // ID로 단건 조회 (항목 포함)
    Task<RewardTable?> GetByIdWithEntriesAsync(int id);

    // 새 보상 테이블 추가
    Task AddAsync(RewardTable table);

    // 변경사항 저장
    Task SaveChangesAsync();
}
