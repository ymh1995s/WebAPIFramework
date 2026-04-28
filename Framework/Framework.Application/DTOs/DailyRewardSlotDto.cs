namespace Framework.Application.DTOs;

// 슬롯 단건 Day 조회/응답 DTO
public record DailyRewardSlotDayDto(
    string Slot,    // "Current" 또는 "Next"
    int Day,        // 1~28
    int? ItemId,    // null이면 보상 없음
    int ItemCount,  // 보상 수량
    DateTime UpdatedAt
);

// 슬롯 Day 수정 요청 DTO (PUT body)
public record UpdateSlotDayDto(
    int? ItemId,    // null이면 보상 없음으로 설정
    int ItemCount   // 0이면 보상 없음으로 설정
);
