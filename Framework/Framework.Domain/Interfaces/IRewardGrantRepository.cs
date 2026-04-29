using Framework.Domain.Entities;
using Framework.Domain.Enums;

namespace Framework.Domain.Interfaces;

// 보상 지급 이력 저장소 인터페이스 — 멱등성 검사 핵심
public interface IRewardGrantRepository
{
    // 이미 지급된 보상인지 확인 (PlayerId + SourceType + SourceKey 조합)
    Task<RewardGrant?> FindAsync(int playerId, RewardSourceType sourceType, string sourceKey);

    // 보상 지급 이력 추가
    Task AddAsync(RewardGrant grant);

    // 보상 지급 이력 삭제 — 지급 실패 시 선기록 롤백에 사용
    Task DeleteAsync(RewardGrant grant);

    // Admin 필터 검색 — 페이지네이션, 기간, 플레이어 등 조건
    Task<(List<RewardGrant> Items, int TotalCount)> SearchAsync(
        int? playerId, RewardSourceType? sourceType, string? sourceKey,
        DateTime? from, DateTime? to, int page, int pageSize);

    // ID로 단건 조회
    Task<RewardGrant?> GetByIdAsync(int id);

    // 변경사항 저장
    Task SaveChangesAsync();
}
