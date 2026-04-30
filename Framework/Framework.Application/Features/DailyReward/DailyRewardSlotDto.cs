namespace Framework.Application.Features.DailyReward;

// 슬롯 단건 Day 조회/응답 DTO
public record DailyRewardSlotDayDto(
    string Slot,    // "Current" 또는 "Next"
    int Day,        // 1~28
    int? ItemId,    // null이면 보상 없음
    int ItemCount,  // 보상 수량
    DateTime UpdatedAt
);

// 일괄 수정 요청 단건 항목 DTO (Day 하나의 변경 내용)
public record UpdateSlotBatchItemDto(
    int Day,        // 1~28
    int? ItemId,    // null이면 보상 없음으로 설정
    int ItemCount   // ItemId가 null이면 0으로 강제됨
);

// 슬롯 일괄 수정 요청 DTO (PUT body)
// Days 목록이 빈 경우 변경 없음으로 처리
public record UpdateSlotBatchDto(
    List<UpdateSlotBatchItemDto> Days
);
