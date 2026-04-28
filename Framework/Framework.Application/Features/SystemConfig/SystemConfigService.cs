using Framework.Domain.Constants;
using Framework.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Framework.Application.Features.SystemConfig;

// 시스템 설정 서비스 구현체
public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _repository;
    private readonly IMemoryCache _cache;

    // 점검 상태 캐시 키 — 매 요청마다 미들웨어에서 확인되므로 짧은 TTL로 DB 부하를 줄임
    private const string MaintenanceCacheKey = "system:maintenance:active";
    // 캐시 TTL — 값 변경 시 즉시 반영되지 않을 수 있으나, Setter에서 무효화하므로 실무상 지연 없음
    private static readonly TimeSpan MaintenanceCacheTtl = TimeSpan.FromSeconds(30);

    public SystemConfigService(ISystemConfigRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    // DB에서 점검 모드 활성화 여부 조회 (수동)
    public async Task<bool> GetMaintenanceModeAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceMode);
        return value == "true";
    }

    // 점검 모드 활성화 여부를 DB에 저장 (수동)
    public async Task SetMaintenanceModeAsync(bool enabled)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceMode, enabled ? "true" : "false");
        await _repository.SaveChangesAsync();
        // 설정 변경 시 캐시 무효화 — 다음 조회에서 최신 값 반영
        _cache.Remove(MaintenanceCacheKey);
    }

    // 점검 예약 시작 시각 조회 (없으면 null)
    public async Task<DateTime?> GetMaintenanceStartAtAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceStartAt);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    // 점검 예약 시작 시각 저장 (null이면 삭제)
    public async Task SetMaintenanceStartAtAsync(DateTime? dateTime)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceStartAt, dateTime?.ToString("O") ?? "");
        await _repository.SaveChangesAsync();
        _cache.Remove(MaintenanceCacheKey);
    }

    // 점검 예약 종료 시각 조회 (없으면 null)
    public async Task<DateTime?> GetMaintenanceEndAtAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.MaintenanceEndAt);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    // 점검 예약 종료 시각 저장 (null이면 삭제)
    public async Task SetMaintenanceEndAtAsync(DateTime? dateTime)
    {
        await _repository.SetValueAsync(SystemConfigKeys.MaintenanceEndAt, dateTime?.ToString("O") ?? "");
        await _repository.SaveChangesAsync();
        _cache.Remove(MaintenanceCacheKey);
    }

    // 클라이언트 앱 강제 업데이트 기준 최소 버전 조회 (미설정 시 "0.0.0" — 강제 업데이트 없음)
    // 서버 버전이 아닌 앱스토어에 배포된 Unity 클라이언트 빌드 버전 기준
    public async Task<string> GetClientAppMinVersionAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.ClientAppMinVersion);
        return string.IsNullOrEmpty(value) ? "0.0.0" : value;
    }

    // 클라이언트 앱 강제 업데이트 기준 최소 버전 저장
    public async Task SetClientAppMinVersionAsync(string version)
    {
        await _repository.SetValueAsync(SystemConfigKeys.ClientAppMinVersion, version);
        await _repository.SaveChangesAsync();
    }

    // 앱스토어에 배포된 클라이언트 앱 최신 버전 조회 (미설정 시 빈 문자열)
    public async Task<string> GetClientAppLatestVersionAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.ClientAppLatestVersion);
        return value ?? "";
    }

    // 앱스토어에 배포된 클라이언트 앱 최신 버전 저장
    public async Task SetClientAppLatestVersionAsync(string version)
    {
        await _repository.SetValueAsync(SystemConfigKeys.ClientAppLatestVersion, version);
        await _repository.SaveChangesAsync();
    }

    // 일일 로그인 보상 하루 기준 시각 — 시(KST) 조회 (미설정 시 기본값 0)
    public async Task<int> GetDailyRewardDayBoundaryHourKstAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyRewardDayBoundaryHourKst);
        return int.TryParse(value, out var hour) ? hour : 0;
    }

    // 일일 로그인 보상 하루 기준 시각 — 분(KST) 조회 (미설정 시 기본값 0)
    public async Task<int> GetDailyRewardDayBoundaryMinuteKstAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyRewardDayBoundaryMinuteKst);
        return int.TryParse(value, out var minute) ? minute : 0;
    }

    // 일일 로그인 보상 하루 기준 시각 — 시(KST) 저장
    public async Task SetDailyRewardDayBoundaryHourKstAsync(int hour)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyRewardDayBoundaryHourKst, hour.ToString());
        await _repository.SaveChangesAsync();
    }

    // 일일 로그인 보상 하루 기준 시각 — 분(KST) 저장
    public async Task SetDailyRewardDayBoundaryMinuteKstAsync(int minute)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyRewardDayBoundaryMinuteKst, minute.ToString());
        await _repository.SaveChangesAsync();
    }

    // 월 28회 초과 시 지급할 기본 보상 아이템 ID 조회 (빈 문자열 또는 미설정 시 null 반환)
    public async Task<int?> GetDailyRewardDefaultItemIdAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyRewardDefaultItemId);
        if (string.IsNullOrEmpty(value)) return null;
        return int.TryParse(value, out var itemId) ? itemId : null;
    }

    // 기본 보상 아이템 ID 저장 (null이면 빈 문자열로 저장 = 미설정 상태)
    public async Task SetDailyRewardDefaultItemIdAsync(int? itemId)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyRewardDefaultItemId, itemId.HasValue ? itemId.Value.ToString() : "");
        await _repository.SaveChangesAsync();
    }

    // 기본 보상 아이템 수량 조회 (미설정 시 0 반환)
    public async Task<int> GetDailyRewardDefaultItemCountAsync()
    {
        var value = await _repository.GetValueAsync(SystemConfigKeys.DailyRewardDefaultItemCount);
        return int.TryParse(value, out var count) ? count : 0;
    }

    // 기본 보상 아이템 수량 저장
    public async Task SetDailyRewardDefaultItemCountAsync(int count)
    {
        await _repository.SetValueAsync(SystemConfigKeys.DailyRewardDefaultItemCount, count.ToString());
        await _repository.SaveChangesAsync();
    }

    // 수동 ON 또는 현재 시각이 예약 범위 안에 있으면 점검 중으로 판단
    // [성능] 미들웨어에서 매 요청마다 호출되므로, 짧은 TTL 메모리 캐시로 DB 3회 조회 비용 제거
    public async Task<bool> IsUnderMaintenanceAsync()
    {
        if (_cache.TryGetValue<bool>(MaintenanceCacheKey, out var cached))
            return cached;

        var result = await ComputeMaintenanceAsync();
        _cache.Set(MaintenanceCacheKey, result, MaintenanceCacheTtl);
        return result;
    }

    // 실제 점검 여부 판정 로직 — 캐시 미스 시에만 호출
    private async Task<bool> ComputeMaintenanceAsync()
    {
        if (await GetMaintenanceModeAsync()) return true;

        var start = await GetMaintenanceStartAtAsync();
        var end = await GetMaintenanceEndAtAsync();
        var now = DateTime.UtcNow;

        return start.HasValue && end.HasValue && now >= start.Value && now <= end.Value;
    }
}
