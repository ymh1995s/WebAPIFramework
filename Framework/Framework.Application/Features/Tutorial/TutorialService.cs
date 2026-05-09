using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Tutorial;

// 튜토리얼 진행 상태 서비스 구현체
public class TutorialService : ITutorialService
{
    // 플레이어당 저장 가능한 최대 행 수
    private const int MaxRowsPerPlayer = 200;
    // Key 최대 길이
    private const int MaxKeyLength = 128;
    // Value 최대 길이
    private const int MaxValueLength = 512;

    private readonly ITutorialProgressRepository _repo;
    private readonly ILogger<TutorialService> _logger;

    public TutorialService(ITutorialProgressRepository repo, ILogger<TutorialService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // 플레이어의 전체 튜토리얼 진행 상태 조회
    public async Task<Dictionary<string, string>> GetProgressAsync(int playerId)
        => await _repo.GetAllByPlayerIdAsync(playerId);

    // 튜토리얼 진행 상태 key-value upsert
    // 기존 키: 값만 갱신, 신규 키: 행 상한 확인 후 삽입
    public async Task<TutorialUpsertResult> UpsertProgressAsync(int playerId, string key, string value)
    {
        // 길이 유효성 검사
        if (key.Length > MaxKeyLength)
            return new TutorialUpsertResult(TutorialUpsertStatus.KeyTooLong);

        if (value.Length > MaxValueLength)
            return new TutorialUpsertResult(TutorialUpsertStatus.ValueTooLong);

        var existing = await _repo.GetByPlayerAndKeyAsync(playerId, key);

        if (existing is not null)
        {
            // 기존 키 업데이트 — 행 수 변화 없으므로 한도 확인 불필요
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            await _repo.SaveChangesAsync();
            return new TutorialUpsertResult(TutorialUpsertStatus.Success);
        }

        // 신규 키 삽입 — 행 상한 확인
        var count = await _repo.CountByPlayerIdAsync(playerId);
        if (count >= MaxRowsPerPlayer)
        {
            _logger.LogWarning(
                "튜토리얼 행 상한 초과 — PlayerId={PlayerId}, 현재={Count}",
                playerId, count);
            return new TutorialUpsertResult(TutorialUpsertStatus.LimitExceeded);
        }

        await _repo.AddAsync(new TutorialProgress
        {
            PlayerId = playerId,
            Key = key,
            Value = value,
            UpdatedAt = DateTime.UtcNow
        });
        await _repo.SaveChangesAsync();
        return new TutorialUpsertResult(TutorialUpsertStatus.Success);
    }
}
