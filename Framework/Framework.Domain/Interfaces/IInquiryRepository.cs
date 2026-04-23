using Framework.Domain.Entities;

namespace Framework.Domain.Interfaces;

// 문의 저장소 인터페이스
public interface IInquiryRepository
{
    // 특정 플레이어의 문의 목록 조회 (최신순)
    Task<List<Inquiry>> GetByPlayerIdAsync(int playerId);

    // ID로 단건 조회
    Task<Inquiry?> GetByIdAsync(int id);

    // 전체 문의 목록 조회 (Admin용, 플레이어 정보 포함)
    Task<List<Inquiry>> GetAllAsync();

    // 문의 추가
    Task AddAsync(Inquiry inquiry);

    // 변경사항 저장
    Task SaveChangesAsync();
}
