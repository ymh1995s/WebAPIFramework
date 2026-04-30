using Framework.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Framework.Application.Features.Exp;

// 레벨 테이블 런타임 제공자 — Singleton 등록, IMemoryCache로 DB 로드 결과를 캐싱
// Singleton이므로 ILevelThresholdRepository(Scoped)를 직접 주입받지 않고 IServiceScopeFactory로 범위 생성
public class LevelTableProvider : ILevelTableProvider
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LevelTableProvider> _logger;

    // 캐시 키 — 레벨 테이블 데이터를 저장하는 키
    private const string CacheKey = "LevelThresholds";

    // 캐시 TTL — Admin에서 교체 시 Invalidate()를 호출하므로 TTL은 장시간으로 설정
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public LevelTableProvider(
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        ILogger<LevelTableProvider> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // 현재 테이블의 최대 레벨
    public int MaxLevel => GetTable().MaxLevel;

    // 누적 경험치로 현재 레벨을 계산
    public int CalcLevel(int totalExp) => GetTable().CalcLevel(totalExp);

    // 특정 레벨의 누적 경험치 임계값 반환
    public int GetThreshold(int level) => GetTable().GetThreshold(level);

    // 캐시 무효화 — Admin에서 테이블을 변경한 뒤 호출
    public void Invalidate()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("LevelTableProvider: 캐시 무효화됨");
    }

    // 캐시에서 테이블 조회 — 미스 시 DB에서 동기 로드 (Singleton 컨텍스트)
    private CachedLevelTable GetTable()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return LoadFromDb();
        })!;
    }

    // DB에서 레벨 테이블 로드 — IServiceScopeFactory로 Scoped 저장소 해결
    private CachedLevelTable LoadFromDb()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILevelThresholdRepository>();

        // 동기 호출 (GetOrCreate 콜백은 동기) — 비동기 버전 필요 시 별도 처리
        var items = repo.GetAllOrderedAsync().GetAwaiter().GetResult();

        if (items.Count == 0)
        {
            // 테이블이 비어 있으면 최소 기본값으로 동작 (오류 로그 기록)
            _logger.LogError("LevelTableProvider: LevelThresholds 테이블이 비어 있습니다. MaxLevel=1로 폴백합니다.");
            return new CachedLevelTable(new Dictionary<int, int> { { 1, 0 } });
        }

        // 레벨 → RequiredExp 딕셔너리 구성
        var map = items.ToDictionary(t => t.Level, t => t.RequiredExp);
        _logger.LogDebug("LevelTableProvider: 레벨 테이블 로드 완료 — MaxLevel: {MaxLevel}", items.Max(t => t.Level));

        return new CachedLevelTable(map);
    }

    // 캐시에 저장되는 레벨 테이블 내부 클래스
    private sealed class CachedLevelTable
    {
        // 레벨 → 누적 경험치 맵 (1부터 MaxLevel까지)
        private readonly Dictionary<int, int> _thresholds;

        public int MaxLevel { get; }

        public CachedLevelTable(Dictionary<int, int> thresholds)
        {
            _thresholds = thresholds;
            MaxLevel = thresholds.Keys.Max();
        }

        // 누적 경험치로 레벨 계산 — 높은 레벨부터 내려오면서 첫 번째 도달 레벨 반환
        public int CalcLevel(int totalExp)
        {
            var level = 1;
            for (var i = MaxLevel; i >= 2; i--)
            {
                if (_thresholds.TryGetValue(i, out var threshold) && totalExp >= threshold)
                {
                    level = i;
                    break;
                }
            }
            return level;
        }

        // 특정 레벨의 임계값 반환 — 없으면 int.MaxValue (해당 레벨 도달 불가 처리)
        public int GetThreshold(int level)
        {
            if (_thresholds.TryGetValue(level, out var threshold))
                return threshold;
            return int.MaxValue;
        }
    }
}
