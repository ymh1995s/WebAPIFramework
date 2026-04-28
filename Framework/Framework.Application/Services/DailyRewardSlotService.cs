using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Constants;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

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

    // 특정 슬롯의 특정 Day 보상 수정
    public async Task UpdateSlotDayAsync(string slot, int day, UpdateSlotDayDto dto)
    {
        // day 범위 검증 (1~28)
        if (day < 1 || day > 28)
            throw new ArgumentOutOfRangeException(nameof(day), "Day는 1~28 사이여야 합니다.");

        // ItemId가 없으면 ItemCount도 0으로 강제 설정
        var itemCount = dto.ItemId.HasValue ? dto.ItemCount : 0;

        await _slotRepository.UpdateSlotDayAsync(slot, day, dto.ItemId, itemCount);
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
