using Framework.Application.Features.SystemConfig;
using Framework.Domain.Constants;
using Framework.Domain.Entities;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Framework.Tests.Unit.SystemConfig;

// SystemConfigService 단위 테스트 — GetClientConfigsStrippedAsync 검증
// IMemoryCache는 실 객체(MemoryCache) 사용 — mock 불가
public class SystemConfigServiceTests
{
    // ── 공통 대리자 ──────────────────────────────────────────────────────────

    private readonly ISystemConfigRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly SystemConfigService _sut;

    // "client." 접두사가 붙은 기본 테스트 데이터 — mock repository 반환값
    private static readonly List<Framework.Domain.Entities.SystemConfig> DefaultClientConfigs = new()
    {
        new Framework.Domain.Entities.SystemConfig { Key = "client.feature_flag", Value = "true" },
        new Framework.Domain.Entities.SystemConfig { Key = "client.max_level",    Value = "99" },
        new Framework.Domain.Entities.SystemConfig { Key = "client.banner_url",   Value = "https://cdn.example/banner.png" },
    };

    public SystemConfigServiceTests()
    {
        _repo  = Substitute.For<ISystemConfigRepository>();
        // IMemoryCache는 실 객체 사용 — NSubstitute로 인터페이스 mock 시 TryGetValue 시그니처 충돌 가능
        _cache = new MemoryCache(new MemoryCacheOptions());

        _sut = new SystemConfigService(_repo, _cache);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    // 기본 데이터를 반환하도록 mock repository 설정
    private void SetupDefaultRepoResponse()
    {
        _repo.GetByPrefixAsync(SystemConfigKeys.ClientConfigPrefix)
             .Returns(DefaultClientConfigs);
    }

    // ── 1. "client." 접두사로만 GetByPrefixAsync 1회 호출 ─────────────────

    [Fact]
    public async Task GetClientConfigsStrippedAsync_ReturnsOnlyClientPrefixedKeys()
    {
        // Arrange: "client." prefix로 목록 반환
        SetupDefaultRepoResponse();

        // Act
        await _sut.GetClientConfigsStrippedAsync();

        // Assert: 정확히 ClientConfigPrefix("client.")로 1회 호출되었는지 검증
        await _repo.Received(1).GetByPrefixAsync(SystemConfigKeys.ClientConfigPrefix);
    }

    // ── 2. 반환된 dict 키에서 "client." 접두사가 제거되었는지 검증 ───────────

    [Fact]
    public async Task GetClientConfigsStrippedAsync_StripsClientPrefixFromKeys()
    {
        // Arrange
        SetupDefaultRepoResponse();

        // Act
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: 반환된 키에 "client." 접두사가 없어야 함
        Assert.All(result.Keys, key => Assert.DoesNotContain("client.", key));
        // 예상 키 존재 여부 검증
        Assert.True(result.ContainsKey("feature_flag"));
        Assert.True(result.ContainsKey("max_level"));
        Assert.True(result.ContainsKey("banner_url"));
    }

    // ── 3. 값은 원본 그대로 보존되어야 함 ────────────────────────────────

    [Fact]
    public async Task GetClientConfigsStrippedAsync_PreservesValuesAsIs()
    {
        // Arrange
        SetupDefaultRepoResponse();

        // Act
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: 원본 값이 그대로 반환되어야 함
        Assert.Equal("true", result["feature_flag"]);
        Assert.Equal("99", result["max_level"]);
        Assert.Equal("https://cdn.example/banner.png", result["banner_url"]);
    }

    // ── 4. 골든 가드 — 서버 전용 키가 결과에 포함되어선 안 됨 ─────────────
    // 이 테스트는 prefix 호출 자체가 1차 방어선임을 전제로,
    // 만약 서버 키가 실수로 섞인다면 즉시 탐지하기 위한 안전망 역할

    [Fact]
    public async Task GetClientConfigsStrippedAsync_NeverContainsServerKeys()
    {
        // Arrange: 서버 키는 mock 응답에 포함시키지 않음 (정상 동작 검증)
        SetupDefaultRepoResponse();

        // Act
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: 알려진 서버 전용 키가 결과 키에 포함되지 않아야 함
        var serverOnlyKeys = new[]
        {
            "maintenance_mode",
            "client_app_min_version",
            "daily_reward_default_item_id",
            "daily_reward_active_month",
            "maintenance_start_at",
            "maintenance_end_at",
        };

        foreach (var serverKey in serverOnlyKeys)
        {
            Assert.DoesNotContain(serverKey, result.Keys);
        }
    }

    // ── 5. 빈 리스트 반환 시 빈 dict, 예외 없음 ─────────────────────────

    [Fact]
    public async Task GetClientConfigsStrippedAsync_EmptyRepository_ReturnsEmptyDictionary()
    {
        // Arrange: repository가 빈 목록 반환
        _repo.GetByPrefixAsync(SystemConfigKeys.ClientConfigPrefix)
             .Returns(new List<Framework.Domain.Entities.SystemConfig>());

        // Act
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: 빈 dict 반환, 예외 없음
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── 6. 항목 수가 repository 반환값과 일치해야 함 ───────────────────

    [Fact]
    public async Task GetClientConfigsStrippedAsync_KeyCountMatchesRepositoryCount()
    {
        // Arrange
        SetupDefaultRepoResponse();

        // Act
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: mock이 3개를 반환했으므로 결과도 3개이어야 함
        Assert.Equal(DefaultClientConfigs.Count, result.Count);
    }

    // ── 7. "client." 자체가 키인 경우 빈 문자열 키로 반환되고 예외 없음 ────
    // 실 데이터 입력 책임은 Admin 측 — 서버는 있는 값을 그대로 변환만 담당

    [Fact]
    public async Task GetClientConfigsStrippedAsync_PrefixOnlyKey_ReturnsEmptyKey()
    {
        // Arrange: "client." 자체가 키인 비정상 데이터 (Admin이 잘못 입력한 경우 방어)
        _repo.GetByPrefixAsync(SystemConfigKeys.ClientConfigPrefix)
             .Returns(new List<Framework.Domain.Entities.SystemConfig>
             {
                 new Framework.Domain.Entities.SystemConfig { Key = "client.", Value = "some-value" }
             });

        // Act: 예외 없이 정상 처리
        var result = await _sut.GetClientConfigsStrippedAsync();

        // Assert: "client." → 빈 문자열 키로 변환, 예외 없음
        Assert.True(result.ContainsKey(string.Empty));
        Assert.Equal("some-value", result[string.Empty]);
    }
}
