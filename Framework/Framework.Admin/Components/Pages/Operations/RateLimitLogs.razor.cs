using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 보안 감시(Rate Limit 로그) 페이지 코드-비하인드.
/// 로그 기록 조건: 동일 IP가 인증 엔드포인트(/auth/guest)에 1분 내 10회를 초과 요청할 때 서버에 자동 기록됨.
/// 이 페이지에서는 IP별 누적 초과 횟수·마지막 발생 시각 조회 및 침투 시도 테스트를 제공한다.
/// </summary>
public partial class RateLimitLogs : SafeComponentBase
{
    // 의존성 주입
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // 로그 조회 상태
    private bool isLoading;
    private List<RateLimitLogDto>? logs;
    private string? loadError;

    // 침투 시도 상태
    private bool isAttacking;
    private List<AttackResult> attackResults = [];

    /// <summary>Rate Limit 초과 로그 목록 조회 — 서버에 누적된 IP별 초과 기록을 반환</summary>
    private async Task LoadLogs()
    {
        isLoading = true;
        loadError = null;

        var client = HttpClientFactory.CreateClient("ApiClient");
        var response = await client.GetAsync(ApiRoutes.AdminRateLimitLogs.Collection);

        if (response.IsSuccessStatusCode)
            logs = await response.Content.ReadFromJsonAsync<List<RateLimitLogDto>>();
        else
            loadError = $"조회 실패: {response.StatusCode}";

        isLoading = false;
    }

    /// <summary>
    /// 침투 시도 테스트 — auth 엔드포인트에 15회 연속 요청하여 Rate Limit(10회/분) 초과를 유도.
    /// 11번째 요청부터 HTTP 429 응답이 반환되고, 서버에 해당 IP의 로그가 기록된다.
    /// </summary>
    private async Task RunAttackTest()
    {
        isAttacking = true;
        attackResults = [];

        var client = HttpClientFactory.CreateClient("ApiClient");
        var payload = new { DeviceId = "attack-test-device" };

        for (var i = 1; i <= 15; i++)
        {
            var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Guest, payload);
            var status = (int)response.StatusCode;
            attackResults.Add(new AttackResult(i, status, status == 429 ? "Too Many Requests" : "OK"));
            StateHasChanged();
        }

        isAttacking = false;

        // 테스트 후 로그 자동 갱신
        await LoadLogs();
    }

    // API 응답 매핑용 로컬 DTO
    private record RateLimitLogDto(string IpAddress, int Count, DateTime LastOccurredAt);
    private record AttackResult(int Index, int Status, string Label);
}
