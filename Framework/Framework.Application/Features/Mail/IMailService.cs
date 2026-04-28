using Framework.Domain.Entities;

namespace Framework.Application.Features.Mail;

// 우편 서비스 인터페이스
public interface IMailService
{
    // 내 우편함 조회 (JWT에서 추출한 PlayerId 기준)
    Task<List<MailDto>> GetMyMailsAsync(int playerId);
    // 단일 플레이어에게 우편 발송
    Task SendAsync(SendMailDto dto);
    // 전체 플레이어에게 우편 일괄 발송
    Task BulkSendAsync(BulkSendMailDto dto);
    // 우편 수령 → 아이템을 인벤토리에 추가 (본인 우편 여부는 playerId로 검증)
    Task<bool> ClaimAsync(int mailId, int playerId);
    // 다수 Mail 엔티티를 컨텍스트에 추가 (배치 발송 시 사용 — SaveChanges 별도 호출 필요)
    Task AddRangeMailsAsync(IEnumerable<Domain.Entities.Mail> mails);
}
