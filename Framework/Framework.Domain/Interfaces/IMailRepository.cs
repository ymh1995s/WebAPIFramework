using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 우편 저장소 인터페이스
public interface IMailRepository
{
    // 특정 플레이어의 전체 우편 조회
    Task<List<Mail>> GetByPlayerIdAsync(int playerId);
    // ID로 단건 조회
    Task<Mail?> GetByIdAsync(int id);
    // 단건 우편 추가
    Task AddAsync(Mail mail);
    // 다수 우편 일괄 추가 (전체 발송 시 사용)
    Task AddRangeAsync(IEnumerable<Mail> mails);
    // 변경사항 저장
    Task SaveChangesAsync();
}
