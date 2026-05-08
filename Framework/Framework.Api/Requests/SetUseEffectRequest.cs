namespace Framework.Api.Requests;

// 아이템 사용 효과 RewardTable ID 설정 요청 DTO
// RewardTableId: null이면 보상 없음으로 초기화, 값이 있으면 지정 테이블의 보상 지급
public record SetUseEffectRequest(int? RewardTableId);
