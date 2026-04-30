using Framework.Domain.Interfaces;

// 엔티티 Shout과 네임스페이스 Shout 충돌 방지를 위해 별칭 사용
using ShoutEntity = Framework.Domain.Entities.Shout;

namespace Framework.Application.Features.Shout;

// 1회 공지 서비스 구현체
public class ShoutService : IShoutService
{
    private readonly IShoutRepository _shoutRepository;
    private readonly IPlayerRepository _playerRepository;

    public ShoutService(IShoutRepository shoutRepository, IPlayerRepository playerRepository)
    {
        _shoutRepository = shoutRepository;
        _playerRepository = playerRepository;
    }

    // 클라이언트용 — 접속 시 활성 1회 공지 목록 반환 (전체 대상 + 개인 대상 포함)
    public async Task<List<ShoutDto>> GetActiveForPlayerAsync(int playerId)
    {
        var shouts = await _shoutRepository.GetActiveForPlayerAsync(playerId);
        return shouts.Select(s => new ShoutDto(s.Id, s.Message, s.CreatedAt, s.ExpiresAt)).ToList();
    }

    // Admin용 — 1회 공지 생성
    // 검증: 메시지 비면 예외, 만료 시간 범위 초과 예외, PlayerId 지정 시 존재 여부 확인
    public async Task<ShoutAdminDto> CreateAsync(CreateShoutDto dto)
    {
        // 메시지 필수 검사
        if (string.IsNullOrWhiteSpace(dto.Message))
            throw new ArgumentException("메시지를 입력해주세요.");

        // 만료 시간 범위 검사 (1분 ~ 7일)
        if (dto.ExpiresInMinutes <= 0 || dto.ExpiresInMinutes > 10080)
            throw new ArgumentException("만료 시간은 1분~7일(10080분) 사이여야 합니다.");

        // 특정 플레이어 대상인 경우 플레이어 존재 여부 확인
        if (dto.PlayerId.HasValue)
        {
            var player = await _playerRepository.GetByIdAsync(dto.PlayerId.Value);
            if (player is null)
                throw new KeyNotFoundException("존재하지 않는 플레이어입니다.");
        }

        var now = DateTime.UtcNow;
        // 엔티티 별칭 사용 — 네임스페이스 충돌 방지
        var shout = new ShoutEntity
        {
            PlayerId = dto.PlayerId,
            Message = dto.Message,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(dto.ExpiresInMinutes),
            IsActive = true
        };

        await _shoutRepository.AddAsync(shout);
        await _shoutRepository.SaveChangesAsync();

        // 저장 후 DTO 변환 반환
        return new ShoutAdminDto(shout.Id, shout.PlayerId, shout.Message, shout.CreatedAt, shout.ExpiresAt, shout.IsActive);
    }

    // Admin용 — 이력 조회 (필터 + 페이지네이션)
    public async Task<ShoutListResponse> GetAllAsync(int page, int pageSize, int? playerId, bool? activeOnly)
    {
        var (items, total) = await _shoutRepository.SearchAsync(playerId, activeOnly, page, pageSize);
        var dtos = items.Select(s => new ShoutAdminDto(s.Id, s.PlayerId, s.Message, s.CreatedAt, s.ExpiresAt, s.IsActive)).ToList();
        return new ShoutListResponse(dtos, total);
    }

    // Admin용 — 1회 공지 즉시 비활성화
    // 존재하지 않으면 false 반환, 성공 시 true 반환
    public async Task<bool> DeactivateAsync(int id)
    {
        var shout = await _shoutRepository.GetByIdAsync(id);
        if (shout is null) return false;

        // 이미 비활성화된 경우에도 true 반환 (멱등성)
        shout.IsActive = false;
        await _shoutRepository.SaveChangesAsync();
        return true;
    }
}
