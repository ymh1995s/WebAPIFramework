using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;

namespace Framework.Application.Services;

// 플레이어 기록 비즈니스 로직 구현체
public class PlayerRecordService : IPlayerRecordService
{
    private readonly IPlayerRecordRepository _repository;

    public PlayerRecordService(IPlayerRecordRepository repository)
    {
        _repository = repository;
    }

    // 전체 목록을 DTO로 변환하여 반환
    public async Task<List<PlayerRecordDto>> GetAllAsync()
    {
        var records = await _repository.GetAllAsync();
        return records.Select(r => new PlayerRecordDto(r.Id, r.PlayerId, r.PlayTime, r.Score, r.CreatedAt)).ToList();
    }

    // 페이지 단위 조회
    public async Task<PagedResultDto<PlayerRecordDto>> GetPagedAsync(int page, int pageSize)
    {
        var records = await _repository.GetPagedAsync(page, pageSize);
        var total = await _repository.GetCountAsync();
        var items = records.Select(r => new PlayerRecordDto(r.Id, r.PlayerId, r.PlayTime, r.Score, r.CreatedAt)).ToList();
        return new PagedResultDto<PlayerRecordDto>(items, total, page, pageSize);
    }

    // ID로 단건 조회, 없으면 null 반환
    public async Task<PlayerRecordDto?> GetByIdAsync(int id)
    {
        var record = await _repository.GetByIdAsync(id);
        if (record is null) return null;
        return new PlayerRecordDto(record.Id, record.PlayerId, record.PlayTime, record.Score, record.CreatedAt);
    }

    // DTO를 엔티티로 변환 후 저장
    public async Task CreateAsync(CreatePlayerRecordDto dto)
    {
        var record = new PlayerRecord
        {
            PlayerId = dto.PlayerId,
            PlayTime = dto.PlayTime,
            Score = dto.Score
        };
        await _repository.AddAsync(record);
        await _repository.SaveChangesAsync();
    }
}
