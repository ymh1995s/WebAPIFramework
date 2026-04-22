using Framework.Application.DTOs;

namespace Framework.Application.Interfaces;

// 우편 서비스 인터페이스
public interface IMailService
{
    // 내 우편함 조회 (JWT에서 추출한 PlayerId 기준)
    Task<List<MailDto>> GetMyMailsAsync(int playerId);
    // 단일 플레이어에게 우편 발송
    Task SendAsync(SendMailDto dto);
    // 전체 플레이어에게 우편 일괄 발송
    Task BulkSendAsync(BulkSendMailDto dto);
    // 우편 수령 → 아이템을 인벤토리에 추가
    Task<bool> ClaimAsync(int mailId);
}
