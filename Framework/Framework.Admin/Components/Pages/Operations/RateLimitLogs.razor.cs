using Framework.Admin.Components;
using Framework.Admin.Constants;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Framework.Admin.Components.Pages.Operations;

/// <summary>
/// 보안 감시(Rate Limit 로그) 페이지 코드-비하인드.
/// Rate Limit 초과 로그 조회 및 침투 시도 테스트를 담당한다.
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

    /// <summary>로그 목록 조회</summary>
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

    /// <summary>auth 엔드포인트에 15회 연속 요청 — Rate Limit 초과 유도</summary>
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
