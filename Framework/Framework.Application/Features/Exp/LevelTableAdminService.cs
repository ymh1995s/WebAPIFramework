using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.Exp;

// 레벨 테이블 Admin 서비스 구현 — 조회, 검증, 일괄 교체 처리
public class LevelTableAdminService : ILevelTableAdminService
{
    private readonly ILevelThresholdRepository _repository;
    private readonly ILevelTableProvider _provider;

    public LevelTableAdminService(
        ILevelThresholdRepository repository,
        ILevelTableProvider provider)
    {
        _repository = repository;
        _provider = provider;
    }

    // 전체 레벨 임계값 목록 조회 (레벨 오름차순)
    public async Task<List<LevelThresholdDto>> GetAllAsync()
    {
        var items = await _repository.GetAllOrderedAsync();
        return items.Select(t => new LevelThresholdDto(t.Level, t.RequiredExp)).ToList();
    }

    // 레벨 테이블 전체 교체 — 검증 후 저장하고 캐시 무효화
    public async Task ReplaceAllAsync(List<LevelThresholdDto> items)
    {
        // 빈 목록 거부
        if (items == null || items.Count == 0)
            throw new ArgumentException("최소 1개 이상의 레벨이 필요합니다.");

        // 레벨 오름차순 정렬
        var sorted = items.OrderBy(i => i.Level).ToList();

        // Level 1부터 연속적으로 시작해야 함
        if (sorted[0].Level != 1)
            throw new ArgumentException("레벨 테이블은 Level 1부터 시작해야 합니다.");

        // Level이 1씩 연속 증가하는지 확인
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Level != sorted[i - 1].Level + 1)
                throw new ArgumentException($"레벨이 연속적이지 않습니다. Level {sorted[i - 1].Level} 다음에 {sorted[i].Level}이 있습니다.");
        }

        // Level 1의 RequiredExp는 반드시 0이어야 함 (시작 레벨)
        if (sorted[0].RequiredExp != 0)
            throw new ArgumentException("Level 1의 RequiredExp는 0이어야 합니다.");

        // 누적 경험치는 단조 증가해야 함 (이전 레벨보다 크거나 같으면 안 됨 — 반드시 더 커야 함)
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].RequiredExp <= sorted[i - 1].RequiredExp)
                throw new ArgumentException(
                    $"Level {sorted[i].Level}의 RequiredExp({sorted[i].RequiredExp})는 " +
                    $"Level {sorted[i - 1].Level}의 RequiredExp({sorted[i - 1].RequiredExp})보다 커야 합니다.");
        }

        // DTO → 엔티티 변환
        var entities = sorted.Select(d => new LevelThreshold
        {
            Level = d.Level,
            RequiredExp = d.RequiredExp,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        // DB 교체 (트랜잭션 내 전체 삭제 → 삽입)
        await _repository.ReplaceAllAsync(entities);

        // 캐시 무효화 — 다음 레벨 계산 시 최신 테이블 반영
        _provider.Invalidate();
    }
}
