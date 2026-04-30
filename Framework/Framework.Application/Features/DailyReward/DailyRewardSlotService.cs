using Framework.Application.Features.SystemConfig;
using Framework.Domain.Constants;
using Framework.Domain.Interfaces;

namespace Framework.Application.Features.DailyReward;

// 일일 보상 슬롯 서비스 구현체
// Current/Next 2슬롯 방식의 보상 관리 및 월 전환 처리를 담당
public class DailyRewardSlotService : IDailyRewardSlotService
{
    private readonly IDailyRewardSlotRepository _slotRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;

    // KST 오프셋 (UTC+9)
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

    public DailyRewardSlotService(
        IDailyRewardSlotRepository slotRepository,
        ISystemConfigRepository systemConfigRepository)
    {
        _slotRepository = slotRepository;
        _systemConfigRepository = systemConfigRepository;
    }

    // 슬롯 전체 28개 Day 조회 후 DTO 변환
    public async Task<List<DailyRewardSlotDayDto>> GetSlotAsync(string slot)
    {
        var rows = await _slotRepository.GetSlotAsync(slot);
        return rows.Select(r => new DailyRewardSlotDayDto(
            r.Slot,
            r.Day,
            r.ItemId,
            r.ItemCount,
            r.UpdatedAt
        )).ToList();
    }

    // 슬롯 전체 Day 보상 일괄 수정 (all-or-nothing 트랜잭션)
    // [검증] day 범위, 중복 Day, ItemId 없을 때 ItemCount=0 강제
    public async Task UpdateSlotAsync(string slot, UpdateSlotBatchDto dto)
    {
        // 변경 항목이 없으면 즉시 반환 (DB 불필요한 접근 방지)
        if (dto.Days is not { Count: > 0 })
            return;

        // day 범위 검증 (1~28)
        var outOfRange = dto.Days.FirstOrDefault(d => d.Day < 1 || d.Day > 28);
        if (outOfRange is not null)
            throw new ArgumentOutOfRangeException(nameof(dto), $"Day는 1~28 사이여야 합니다. (입력값: {outOfRange.Day})");

        // 중복 Day 검증
        var hasDuplicate = dto.Days.GroupBy(d => d.Day).Any(g => g.Count() > 1);
        if (hasDuplicate)
            throw new ArgumentException("중복된 Day 값이 포함되어 있습니다.", nameof(dto));

        // ItemId가 없는 항목은 ItemCount를 0으로 강제 정규화하여 튜플로 변환
        var normalizedItems = dto.Days.Select(item =>
            (item.Day, item.ItemId, ItemCount: item.ItemId.HasValue ? item.ItemCount : 0)
        ).ToList();

        // 일괄 메모리 갱신 후 단일 SaveChanges (부분 실패 시 전체 롤백)
        await _slotRepository.UpdateSlotBatchAsync(slot, normalizedItems);
        await _slotRepository.SaveChangesAsync();
    }

    // 월 전환 체크 — 현재 KST 연월이 SystemConfig의 활성 연월과 다르면 전환 처리
    // [순서] Next → Current 복사 → daily_reward_active_month 갱신
    public async Task EnsureMonthTransitionAsync()
    {
        // 현재 KST 연월 (YYYYMM)
        var kstNow = DateTime.UtcNow + KstOffset;
        var currentMonth = $"{kstNow.Year}{kstNow.Month:D2}";

        // DB에서 활성 연월 조회
        var storedMonth = await _systemConfigRepository.GetValueAsync(SystemConfigKeys.DailyRewardActiveMonth);

        // 같으면 전환 필요 없음
        if (storedMonth == currentMonth) return;

        // 월이 달라짐 — Next 슬롯을 Current로 복사
        await _slotRepository.CopyNextToCurrentAsync();

        // 활성 연월 갱신
        await _systemConfigRepository.SetValueAsync(SystemConfigKeys.DailyRewardActiveMonth, currentMonth);
        await _systemConfigRepository.SaveChangesAsync();
    }
}
