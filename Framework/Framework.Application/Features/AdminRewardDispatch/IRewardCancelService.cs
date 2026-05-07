namespace Framework.Application.Features.AdminRewardDispatch;

// 보상 지급 취소 서비스 인터페이스
// Admin이 잘못 지급된 보상을 취소하고 선택적으로 플레이어에게 안내 우편을 발송함
public interface IRewardCancelService
{
    // 단건 보상 지급 취소
    // grantId: 취소할 RewardGrant ID
    // dto: 취소 사유 및 안내 우편 설정
    // adminId: 취소를 수행하는 Admin ID (감사 목적 — null 허용)
    Task<CancelRewardResult> CancelAsync(int grantId, CancelRewardDto dto, int? adminId);
}
