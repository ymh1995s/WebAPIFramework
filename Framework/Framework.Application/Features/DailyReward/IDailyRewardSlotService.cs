namespace Framework.Application.Features.DailyReward;

// 일일 보상 슬롯 서비스 인터페이스 (Admin CRUD + 월 전환 로직)
public interface IDailyRewardSlotService
{
    // 슬롯 전체 28개 Day 조회
    Task<List<DailyRewardSlotDayDto>> GetSlotAsync(string slot);

    // 슬롯 전체 Day 보상 일괄 수정 (검증 포함, 단일 트랜잭션)
    // 부분 실패 시 전체 롤백 (all-or-nothing)
    Task UpdateSlotAsync(string slot, UpdateSlotBatchDto dto);

    // 월 전환이 필요한지 확인 후 필요하면 Next → Current 복사 및 활성 연월 갱신
    // DailyLoginService에서 발송 전 호출
    Task EnsureMonthTransitionAsync();
}
