namespace Framework.Application.Features.ItemUse;

// 아이템 사용 처리 결과 상태 열거형
public enum ItemUseResultStatus
{
    // 정상 처리 완료 (수량 차감 + 보상 지급 또는 수량 차감만)
    Success,

    // 수량 부족 — 보유 수량이 0 이하
    NotEnoughQuantity,

    // 아이템 미보유 — 플레이어가 해당 아이템을 보유하지 않음
    ItemNotFound,

    // 중복 요청 — 동일 clientRequestId로 이미 처리된 요청
    Duplicate,

    // 보상 테이블 항목 없음 — UseRewardTableId가 지정되어 있으나 테이블 항목이 비어 있음
    RewardTableEmpty,

    // 보상 지급 실패 — RewardDispatcher 오류 또는 플레이어 미존재
    RewardGrantFailed
}

// 아이템 사용 처리 결과 DTO
public record ItemUseResult(ItemUseResultStatus Status);
