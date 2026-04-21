using Framework.Application.DTOs;
using Framework.Application.Interfaces;
using Framework.Application.Options;
using Framework.Domain.Enums;

namespace Framework.Application.Services;

// 기본 매칭 서비스 구현체 - Singleton 등록 필수 (인-메모리 대기 풀 유지)
public class MatchMakingService : IMatchMakingService
{
    // 멀티스레드 동시 접근 방지용 락 객체
    private readonly object _lock = new();

    // Tier별 대기 유저 목록
    private readonly Dictionary<Tier, List<MatchUserDto>> _waitingPool = new();

    private readonly IMatchNotifier _notifier;
    private readonly int _maxPlayers;

    public MatchMakingService(IMatchNotifier notifier, MatchMakingOptions options)
    {
        _notifier = notifier;
        _maxPlayers = options.MaxPlayers;
    }

    // 매칭 참가 - 정원 충족 시 즉시 성사
    public async Task<MatchResultDto> JoinAsync(JoinMatchRequestDto request)
    {
        MatchResultDto result;

        lock (_lock)
        {
            // 전체 대기열에서 중복 유저 확인
            bool isDuplicate = _waitingPool.Values.Any(list => list.Any(u => u.UserId == request.UserId));
            if (isDuplicate)
                return MatchResultDto.Duplicate($"{request.UserId} 는 이미 대기열에 있습니다.");

            if (!_waitingPool.ContainsKey(request.Tier))
                _waitingPool[request.Tier] = [];

            var list = _waitingPool[request.Tier];
            var user = new MatchUserDto(request.UserId, request.Tier, request.HumanType);
            list.Add(user);

            if (list.Count >= _maxPlayers)
            {
                // 요청 유저 포함 정원만큼 추출 후 대기열에서 제거
                var others = list.Where(u => u.UserId != request.UserId).Take(_maxPlayers - 1).ToList();
                var members = others.Prepend(user).ToList();
                foreach (var m in members) list.Remove(m);

                result = MatchResultDto.Matched(members, $"매칭 성사: {string.Join(", ", members.Select(m => m.UserId))}");
            }
            else
            {
                result = MatchResultDto.Waiting(list.Count, $"대기 중... ({list.Count}/{_maxPlayers})");
            }
        }

        // SignalR 알림은 lock 밖에서 비동기 호출
        var tierGroup = request.Tier.ToString();
        if (result.IsMatched)
            await _notifier.NotifyMatchedAsync(tierGroup, result);
        else
            await _notifier.NotifyWaitingAsync(tierGroup, result.WaitingCount, _maxPlayers);

        return result;
    }

    // 매칭 취소 - 대기열에 없으면 null 반환
    public async Task<MatchResultDto?> CancelAsync(string userId)
    {
        Tier? cancelledTier = null;
        int remainingCount = 0;

        lock (_lock)
        {
            foreach (var (tier, list) in _waitingPool)
            {
                var user = list.Find(u => u.UserId == userId);
                if (user is null) continue;

                list.Remove(user);
                cancelledTier = tier;
                remainingCount = list.Count;
                break;
            }
        }

        if (cancelledTier is null) return null;

        await _notifier.NotifyWaitingAsync(cancelledTier.Value.ToString(), remainingCount, _maxPlayers);

        return MatchResultDto.Waiting(remainingCount, $"{userId} 취소 완료. 잔여 대기: {remainingCount}/{_maxPlayers}");
    }
}
